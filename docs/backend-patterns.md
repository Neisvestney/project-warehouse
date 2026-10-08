# Backend Patterns

Recurring implementation patterns used in the server project.

---

## Shared controller logic lives in a service

Logic that more than one controller action needs — or obviously will — goes into a `Services/IXxx.cs` +
`Services/Xxx.cs` pair registered with `AddScoped` in `Program.cs`, never copied into each action. A copy is
where a check gets forgotten, and a missing existence check turns a clean 422 into a 500 from a raw foreign-key
violation.

### Rules

- **The contract returns the error, the controller returns the response.** A service method that can reject
  its input returns `AppProblemDetails?` — `null` on success — and the action hands it to
  `AppControllerBase.Problem(...)`. The service never builds an `IActionResult`.
- **Existence is checked explicitly**, not left to the database constraint: only an `AppProblemDetails` renders
  on the frontend.
- **Shapes shared across entities are interfaces.** The service works against them and knows no concrete
  entity or request type.
- **One method per cardinality** when the same operation exists for a single reference (1:1) and for a list
  (1:N).

### Example

`IDataFileBindingService` is the single way controllers attach files to entities:

```csharp
Task<AppProblemDetails?> BindSingleAsync(
    Guid? fileId, Action<Guid?> assign, string field, CancellationToken ct);

Task<AppProblemDetails?> BindListAsync<TLink, TRequest>(
    IReadOnlyList<TRequest> requests, List<TLink> links, DbSet<TLink> dbSet,
    Action<TLink> setOwner, string field, CancellationToken ct)
    where TLink : class, IDataFileLink
    where TRequest : class, IDataFileLinkRequest;
```

```csharp
var imageProblem = await fileBinding.BindSingleAsync(
    request.MainImageFileId, v => item.MainImageFileId = v, "mainImageFileId", ct);
if (imageProblem is not null) return Problem(imageProblem);
```

---

## Search with `WhereMatchesSearch` + `[Projectable]`

### Pattern

Full-text search across multiple entity fields is done via two pieces:

1. **`[Projectable]` `SearchString` on the domain entity** — concatenates all searchable fields into one string. EF Projectables expands this property inline during SQL translation, so no extra joins or subqueries are generated.
2. **`WhereMatchesSearch(e => e.SearchString, searchString)`** — applies a case-insensitive `ILIKE` filter per token (AND semantics across tokens, OR across fields happens implicitly via the concatenated string).

### Example

**Domain entity** (`CatalogItem.cs`):
```csharp
[Projectable]
public string SearchString =>
    (Name ?? "") + " " + (Article ?? "");
```

**Controller** (`CatalogController.cs`):
```csharp
db.CatalogItems
    .WhereMatchesSearch(c => c.SearchString, searchString)
    ...
```

### Searching across related entities

When the searchable fields span a navigation property, navigate to it directly in the expression. For simple cases (a single field on a related entity) you don't need a separate `SearchString` property — just navigate inline:

**Controller** (`InventoryItemsController.cs`):
```csharp
db.InventoryItems.OfType<UnitInventoryItem>()
    .WhereMatchesSearch(u => u.InventoryNumber, searchString)
    ...
```

For richer cross-field search, put `SearchString` on the related entity and navigate to it:

**Domain entity** (`Receipt.cs`):
```csharp
[Projectable]
public string SearchString => Number + " " + Name + " " + Notes;
```

**Controller** (`ReceiptsController.cs`):
```csharp
db.Receipts
    .WhereMatchesSearch(r => r.SearchString, searchString)
    ...
```

EF Projectables intercepts the `SearchString` member access during LINQ-to-SQL translation and expands it inline — the navigation is transparent to EF Core.

### Searching across a collection — `WhereMatchesExtendedSearch`

A `SearchString` only works while every searchable field sits on a scalar path: `WhereMatchesSearch` splices the
expression straight into `EF.Functions.ILike`, so the result has to be one SQL scalar. **Do not try to fold a
collection into it with `string.Join`** — EF Core 10 does not translate that into `string_agg`; the query falls
back to client evaluation and throws at runtime (checked empirically, not assumed).

Use a `[Projectable]` **predicate** over one ready-made ILIKE pattern instead, and let each collection become its
own `EXISTS` subquery:

**Domain entity** (`Order.cs`):
```csharp
[Projectable]
public bool MatchesExtendedSearch(string pattern) =>
    EF.Functions.ILike(SearchExtensions.Normalize(SearchString), pattern, SearchExtensions.EscapeChar)
    || Boxes.Any(b => EF.Functions.ILike(SearchExtensions.Normalize(b.Label ?? ""), pattern, SearchExtensions.EscapeChar))
    || Boxes.Any(b => b.Components.Any(c =>
        EF.Functions.ILike(SearchExtensions.Normalize(c.CatalogItem.SearchString), pattern, SearchExtensions.EscapeChar)));
```

**Controller** (`OrdersController.cs`):
```csharp
query.WhereMatchesExtendedSearch((o, pattern) => o.MatchesExtendedSearch(pattern), searchString)
```

`Receipt`, `Writeoff` and `Stocktake` follow the same shape over their lines: the line's catalog item (for a
write-off unit line — the unit's catalog item) and the inventory number (a receipt line has none of its own, so
its units are reached through the placements). Their list
endpoints pair it with a `catalogItemIds` filter over the same lines.

`WhereMatchesExtendedSearch` keeps the same contract as `WhereMatchesSearch` — it owns tokenization and `%`, `_`,
`\` escaping, and substitutes the finished pattern into the predicate once per token. Semantics stay **AND across
tokens, OR across sources**. Nested `SearchString` properties on related entities expand normally inside it.

### Cyrillic/Latin look-alikes — `SearchExtensions.Normalize`

Search treats visually identical Cyrillic and Latin letters (`А`/`A`, `Р`/`P`, `С`/`C`, …) as the same letter, so
"PE-100" typed in Latin finds "РЕ-100" stored in Cyrillic and vice versa. `Normalize` folds both upper- and
lowercase Cyrillic look-alikes (`HomoglyphsFrom` → `HomoglyphsTo`) onto Latin; lowercase letters without a visual
twin (`в`, `м`, `н`, `т`, `к`) are folded too, so `ILIKE` keeps matching regardless of case.

- `ToPattern` normalizes every token, so the pattern is always folded.
- `WhereMatchesSearch` wraps the search field in `Normalize` itself.
- In a `MatchesXxxSearch(pattern)` predicate **every column passed to `ILike` must be wrapped in
  `SearchExtensions.Normalize(...)`** — an unwrapped column never matches a Cyrillic token, since the pattern is
  folded and the column is not.
- `Normalize` is registered with `HasDbFunction` in `ApplicationDbContext` and translates to
  `translate(value, '<from>', '<to>')`; the C# body only runs on client evaluation.

Cost: one correlated `EXISTS` per collection per token, with no index behind `ILIKE`. Fine for small or paginated
sets; reach for `pg_trgm` or a materialized column before pointing it at a large table.

### Fuzzy search and relevance order

`WhereMatchesSearch(field, searchString, fuzzy: true)` also lets a row through when the whole normalized query is
trigram-word-similar to the normalized field (`pg_trgm` `<%`, default `word_similarity_threshold` 0.6), so
"PETG чорный" finds "PETG Черный 1 кг". Queries shorter than `SearchExtensions.FuzzyMinLength` (3) stay exact —
shorter ones match almost anything. There is no trigram index, so every row is scored: `fuzzy: true` is for small
tables only.

`OrderBySearchRelevance(field, searchString, thenBy)` puts rows where every token is a substring first, then sorts
by `word_similarity`, then by `thenBy`; without a search it is plain `OrderBy(thenBy)`, and further `ThenBy` calls
chain onto it. `ThenBySearchRelevance(field, searchString)` appends the same two keys to an existing order and is
a no-op without a search — catalog `for-select` uses it to keep archived items last whatever their relevance:
`OrderBy(c => c.IsArchived).ThenBySearchRelevance(...)`.

A selector needs its own non-paginated `for-select` endpoint (`take` instead of `page`/`pageSize`) rather than
reusing the paginated list: the list keeps its table sort, the selector sorts by relevance.

| Where | Filter | Order |
|---|---|---|
| Selectors and autocompletes — `catalog/for-select`, `users/for-select`, `warehouses/for-select`, every `…/tags` endpoint and the tags page (`TagsService`), `organizations/short`, `roles/search` | `fuzzy: true` | `OrderBySearchRelevance` / `ThenBySearchRelevance` |
| Lists of small entities — catalog, stock (`InventoryItemsController` catalog filter), stock forecast (catalog filter), warehouses, users, organizations, marketplace accounts and cards, auto-map rules | `fuzzy: true` | the list's own sort; relevance does not override the user's column sort |
| Document lists (`WhereMatchesExtendedSearch`), inventory numbers, analytics filters | exact | — |

### Rules

- Always use `?? ""` on nullable string fields inside `SearchString` to avoid null propagation in SQL.
- Non-nullable string fields don't strictly need `?? ""`, but it's kept for consistency.
- `WhereMatchesSearch` with a `null` or whitespace `searchString` is a no-op — so is `WhereMatchesExtendedSearch`.
- Token splitting is space-based; each token must appear somewhere in the concatenated string (AND across tokens).
- Scalar fields → `SearchString` + `WhereMatchesSearch`. Anything reached through a collection →
  `MatchesXxxSearch(pattern)` + `WhereMatchesExtendedSearch`. Never `string.Join` over a navigation.
- Both live in `SearchExtensions`; escaping is `SearchExtensions.EscapeChar`, never a bare `"\\"` literal.
- Every column inside a `MatchesXxxSearch` predicate goes through `SearchExtensions.Normalize`.

### Global search — `GlobalSearchService`

`GET /api/commoncontent/search` runs one `UNION ALL` over every searchable type and returns at most 10
`AppEntity`. Each type is a `SearchSource<T>`: the queryable from `IUserQueryFilterService` (so access is part of
the query), an `Id` selector and its `SearchString`. Every source contributes two branches, each projected to a
flat `(Type, Id, Exact, Score)` row and cut to its best 10:

- **exact** — `WhereMatchesSearch` over all visible rows;
- **fuzzy** — `pg_trgm` `<%` (`EF.Functions.TrigramsAreWordSimilar`) of the normalized query against the
  normalized `SearchString`, skipped for queries shorter than `SearchExtensions.FuzzyMinLength`. A source may narrow the rows this
  branch scans: orders pass only the 1000 most recent by `Number`, read backwards off `IX_Orders_Number`.

`Score` is `word_similarity` in both branches. In memory, a row found by both keeps its exact hit, exact hits
rank above fuzzy ones, then by `Score`. The best hit of every type takes a slot first, the remaining slots go by
rank, and the result keeps rank order. Only the picked ids are then loaded with `ProjectTo<AppEntity>`, one query
per type present.

The union cannot carry `AppEntity` itself: its `AdditionalFields` dictionary is a client projection, and EF
translates a set operation only over identical flat columns — hence the `(Type, Id)` round trip.

Adding a type: a `[Projectable] SearchString` on the entity, an `AppEntity` map, a `GetXxxAsync` in
`IUserQueryFilterService`, and one `SearchSource<T>` line in `GlobalSearchService`.

---

## Inheritable fields with `[Projectable]`

Some fields on child entities can inherit a value from a parent if the child's own value is `null`. The resolved "effective" value is exposed in DTOs while the raw nullable value is stored in the database.

### Pattern

1. **Store `T?` on the domain entity** — `null` means "inherit from parent".
2. **Add a `[Projectable]` computed property** that resolves the effective value using the navigation property.
3. **Map the `[Projectable]` in `AppMapperProfile`** so DTOs always carry the resolved value.

### Example

**Domain entity** (`CatalogItem.cs`):
```csharp
public string? Notes { get; set; }

[Projectable]
public string? EffectiveNotes => Notes ?? (Group != null ? Group.Notes : null);
```

**Mapper** (`AppMapperProfile.cs`):
```csharp
CreateMap<CatalogItem, CatalogItemDto>()
    .ForMember(d => d.Notes, opt => opt.MapFrom(s => s.EffectiveNotes));
```

### Rules

- Use `[Projectable]` so the resolution works both in-memory (after `Include`) and in EF Core `ProjectTo` queries (translated to SQL).
- Always load the parent navigation property when the entity may need to resolve an inherited value (add to `Include` chains).
- Fields that are non-nullable (e.g. `bool IsArchived`) are **not** inheritable — use a plain default instead.
- Inheritance is one level deep by convention; deeper chains require chaining the `Effective*` properties.

---

## Shared orderings: one extension per shape, `[Projectable]` for Include

An order that several queries must agree on (the catalog order: `IsArchived`, then `FullName`, then `Id`) lives in `SortExtensions` instead of being repeated at each call site:

- **Query root** — a plain `IQueryable<T>` extension, e.g. `db.CatalogItems.OrderByCatalog()`.
- **Collection inside a filtered Include** — an `IEnumerable<T>` extension marked `[Projectable]`, e.g. `.Include(c => c.VariationMembers.InCatalogOrder())`. EF Projectables inlines the method body before EF parses the Include, so it translates to an ordinary `ORDER BY`; without `[Projectable]` EF rejects the unknown method.
- **Rows already in memory** — `OrderLikeCatalog(x => x.CatalogItem)`. It reads `FullName` in memory, so the query that loaded the rows must also include `CatalogItem.Group`.

The name a DTO shows for a catalog item is `FullName` too. A client that re-sorts rows by that name then lands on the same order the server returned; a DTO carrying the bare `Name` makes the client order drift from the catalog.

Keep both shapes side by side in `SortExtensions` so a change to the order touches one place. A filtered Include orders only freshly loaded collections: a collection that is already tracked keeps its in-memory order, so a reload after `SaveChangesAsync` that must come back ordered runs after `db.ChangeTracker.Clear()`.

---

## Updating related entity lists with `IListUpdater`

`IListUpdater` synchronises an in-memory EF Core navigation collection with a list of incoming DTOs using AutoMapper. It handles adds, updates, and deletes in one call, so callers don't need to diff collections manually.

Two overload families exist depending on whether list order is meaningful.

**The wire contract this produces.** Every `PUT` built on the identity-based overload takes the *full desired
list* and diffs it server-side: an element with `id: null` is created, an element with an `id` updates the
existing row, and an existing row absent from the payload is deleted. Clients never send explicit deletes.
Every `PUT` taking a child collection in this codebase follows it.

---

### Index-based overload (ordered lists)

Use when the DTO and entity lists are positionally aligned — i.e. `dto[i]` always corresponds to `source[i]`.

```csharp
void UpdateList<T, TDto>(
    IList<TDto>? dto,
    IList<T>?   source,
    DbSet<T>    dbSet,
    Action<TDto, T>? afterMap = null)

Task UpdateListAsync<T, TDto>(
    IList<TDto>? dto,
    IList<T>?    source,
    DbSet<T>     dbSet,
    Func<TDto, T, Task>? afterMapAsync = null)
```

**Behaviour:**
- For each index `i < dto.Count` and `i < source.Count` — maps the DTO onto the existing entity in-place.
- For each index `i >= source.Count` — creates a new entity via AutoMapper and appends it to `source`.
- If `source.Count > dto.Count` after the loop — removes the trailing entities from `dbSet` and `source`.
- If either list is `null` — does nothing.

**Example:**

```csharp
_listUpdater.UpdateList(dto.Lines, order.Lines, db.OrderLines);
```

---

### Identity-based overload (unordered lists)

Use when items have stable identifiers and position in the list does not determine which entity a DTO maps to.

```csharp
void UpdateList<T, TDto>(
    List<TDto>? dto,
    List<T>?    source,
    DbSet<T>    dbSet,
    Func<T, TDto, bool>  compare,
    Func<TDto, bool>     isNew,
    Action<TDto, T>?     afterMap = null)

Task UpdateListAsync<T, TDto>(
    List<TDto>?           dto,
    List<T>?              source,
    DbSet<T>              dbSet,
    Func<T, TDto, bool>   compare,
    Func<TDto, bool>      isNew,
    Func<TDto, T, Task>?  afterMapAsync = null)
```

**Parameters:**
- `compare(entity, itemDto)` — returns `true` when the entity matches the DTO (e.g. same `Id`).
- `isNew(itemDto)` — returns `true` when the DTO represents a record that does not exist in the DB yet (e.g. `Id == 0`).

**Behaviour:**
1. Iterates `source` in reverse; removes any entity for which no matching DTO exists in `dto` (via `compare`).
2. For each DTO where `isNew` returns `true` — creates a new entity via AutoMapper and appends it to `source`.
3. For each non-new DTO — finds the matching entity via `compare` and maps the DTO onto it in-place; if somehow no match is found, creates a new entity.
- If either list is `null` — does nothing.

**Example:**

```csharp
_listUpdater.UpdateList(
    dto.Items,
    entity.Items,
    db.OrderItems,
    compare: (item, itemDto) => item.Id == itemDto.Id,
    isNew:   itemDto => itemDto.Id == 0);
```

For DTOs with a `Guid?` Id (common on update DTOs where `null` means "not yet persisted"), use `isNew: x => x.Id == null`:

```csharp
_listUpdater.UpdateList(
    dto.Items,
    entity.Items,
    db.OrderItems,
    compare: (item, itemDto) => itemDto.Id != null && item.Id == itemDto.Id,
    isNew:   itemDto => itemDto.Id == null);
```

---

### `afterMap` / `afterMapAsync` callback

Both overloads accept an optional post-mapping callback invoked on every created or updated entity. Use it to set fields that AutoMapper cannot resolve on its own, such as foreign keys or values derived from the parent entity.

```csharp
_listUpdater.UpdateList(
    dto.Lines,
    order.Lines,
    db.OrderLines,
    afterMap: (lineDto, line) => line.OrderId = order.Id);
```

---

### Rules

- Register `IListUpdater` / `ListUpdater` as a scoped service; `ListUpdater` depends on `IMapper`.
- Use the index-based overload only when the client always sends the full ordered list and position is meaningful.
- Use the identity-based overload for named/identified child collections where partial updates or reordering may occur.
- Call `SaveChangesAsync` after `UpdateList` — the method mutates the tracked collection but does not save.

---

## Background work: queue + worker + advisory lock

Use this shape whenever an endpoint must answer immediately but the work takes minutes.

**Never `Task.Run`.** It is not tied to the host lifetime, so a container stop drops in-flight work silently.

1. **A bounded `Channel<T>` behind an interface** (`Integrations/Sync/MarketplaceSyncQueue.cs`).
   `SingleReader = true` serializes the work; `FullMode = Wait` applies backpressure instead of dropping requests.
2. **A `BackgroundService` that drains it** (`MarketplaceSyncWorker.cs`), creating **its own DI scope per item** —
   the request scope is long gone by then, so nothing scoped may be captured from it.
3. **Reconcile on startup.** A job row left in a `running` state by a crash blocks the resource forever, because
   both the UI guard and the scheduler refuse to start a second one. The worker's first action is to fail every
   stale `running` row with a dedicated error code (`marketplaceSyncInterrupted`). Roll back any denormalized
   summary alongside it (`MarketplaceAccount.LastSyncStatus` / `LastSyncError` / `LastSyncAt`) — reconciling only
   the job row leaves the parent entity advertising the outcome of the run before the one that died.
4. **Cross-process exclusivity via a PostgreSQL advisory lock** (`PostgresAdvisoryLock.cs`).
   The lock is **session-scoped**, and Npgsql runs `DISCARD ALL` when a pooled connection is returned — which
   releases it. So it must be taken on a **dedicated `NpgsqlConnection` from the injected `NpgsqlDataSource`**
   and held for the whole run, never on the request's `DbContext` connection.
   That idle connection also needs `Keepalive` in the connection string, or a NAT/firewall may drop the session
   and silently free the lock.
5. **Persist failures structurally.** Store an `AppFieldError` in a `jsonb` column rather than a message string, so
   the client renders from `code` + `args`. Note the enum is serialized there as an integer by Npgsql's serializer,
   not as the camelCase string the MVC options produce — such an `ErrorCode` enum may only be appended to.

The request side keeps a cheap `AnyAsync(... == Running)` check purely for UX (`409`); the advisory lock is what
actually guarantees exclusivity.

**Scheduling** uses Quartz with an in-memory job store and one `[DisallowConcurrentExecution]` *scanning* job that
picks whatever is due (`MarketplaceSyncScanJob.cs`), rather than a trigger per entity — the schedule then needs no
mutation when an interval changes, and a restart cannot lose it.

## File attachments: adding a new attachment point

The mechanism — real FKs instead of a polymorphic table, the `OnDelete` rules, and the GC that derives its
predicate from the EF model — lives in [data-files-specification.md](data-files-specification.md). Read it before
adding an attachment point; what follows is only the checklist.

1. **1:1** — add `Guid? XFileId` + a `DataFile? X` navigation, FK `OnDelete(DeleteBehavior.Restrict)`.
   **1:N** — add a join entity implementing `IDataFileLink`, `Cascade` to the owner, `Restrict` to `DataFile`.
2. Add an AutoMapper map from the request element to the link entity with `Id` ignored; the request element
   implements `IDataFileLinkRequest`.
3. In the controller, bind through `IDataFileBindingService` — never inline the check-and-sync:

```csharp
var problem =
    await fileBinding.BindSingleAsync(request.MainImageFileId,
        v => item.MainImageFileId = v, "mainImageFileId", ct)
    ?? await fileBinding.BindListAsync(request.Images, item.Images, db.CatalogItemImages,
        setOwner: img => img.CatalogItemId = item.Id, field: "images", ct);
if (problem is not null) return Problem(problem);
```

4. There is **no** step for the garbage collector: it reads the foreign keys out of the EF model, so adding the FK
   *is* registering the attachment point.

**A file identifier must never be parked in `jsonb`, a string column, or an array without a FK** — the collector
sees foreign keys and nothing else, and would delete such a file as an orphan.

## A query loading more than one collection picks its splitting mode

**Every query that loads two or more collection navigations — through `Include` or through a projection such as
`ProjectTo` — states `AsSplitQuery()` or `AsSingleQuery()` explicitly.** No global splitting behavior is
configured, so EF logs `MultipleCollectionIncludeWarning` for each such query left unmarked.

**Sibling collections take `AsSplitQuery()`.** EF's single-query mode `JOIN`s every loaded collection together,
so for collections hanging off the same parent the row count is their *product*.
`CatalogController.LoadItemWithDetailsAsync` pulls nine — tags, bundle components, both variation sides, images,
marketplace cards and group children with their own tags and images — and a group of 20 children with 5 images
each multiplies out into six figures of duplicated rows for one item. Split query issues one statement per
collection instead. The same applies to a DTO projection whose mapping reads several collections, as with
`UserDetailDto` (roles, permissions, warehouses) and `WarehouseDto` (storage places, layout objects). A paginated
split query needs a unique `OrderBy`, otherwise the per-collection statements can page over different rows.

**A single chain of nested collections takes `AsSingleQuery()`.** `Fulfillments → BundleComponents` or
`Boxes → Components → Fulfillments` yields one row per leaf, with no multiplication, so splitting would only add
round trips — which matters in hot loops such as `OrderService.IsTaskFullyFulfilledAsync` during batch fulfillment.

---

## `PaginatedWithMeta<T, TMeta>`

**A number describing the whole filtered list travels with the page, not in a second endpoint.**
`PaginatedWithMeta<T, TMeta>` extends `Paginated<T>` with one extra property, `Meta`, and
`paginated.WithMeta(meta)` wraps a page that has already been materialised:

```csharp
var paginated = await query.ProjectTo<ReceiptSummaryDto>(mapper.ConfigurationProvider)
    .ToPaginatedAsync(page, pageSize, ct);

var meta = new StatusListMetaDto<ReceiptStatus> {StatusCounts = await facetQuery.CountByStatusAsync(r => r.Status, ct)};

return Ok(paginated.WithMeta(meta));
```

The aggregates are computed over the **filtered, unpaged, unsorted** query — the one before `Skip`/`Take` —
so they stay stable while the user walks the pages. Each of them is its own round trip, which is why the
meta DTO stays small; a handful of counters over the same filter belongs in one `GroupBy` instead.
`OrdersController.GetAll` groups by `(Status, OverdueKindAt(now))` once over the query without the status and
overdue filters, then derives both the per-status counts and the two overdue counts from those few rows in
memory. The `overdue` filter calls the same `[Projectable]` `Order.OverdueKindAt`, so the counter and the list it
opens cannot disagree. The page total is the sum of the cells matching both filters, passed to the
`ToPaginatedAsync(page, pageSize, total, ct)` overload, which skips its own `COUNT` — on a search the filter is
the expensive part, and every extra query over the filtered set pays for it again.

The component sum is a separate `SumAsync`. Folding it into the grouping — as a correlated subquery, a
pre-aggregated join or a join with `count(DISTINCT)` — is two to fourteen times slower than the plain grouping
plus the plain sum on the FBS list, because each variant aggregates every box component of every order in the
set.

A list whose only facet is its status tabs uses the generic `StatusListMetaDto<TStatus>` (receipts, write-offs,
stocktakes). The controller builds the filtered query **without** the status filter first, applies status on top
of it for the page, and fills the meta with `facetQuery.CountByStatusAsync(x => x.Status, ct)` — one `GroupBy`,
zeros filled in for every enum member, so the client always gets a full set of tabs. `Include`s go on the page
query only; the grouping does not need them.

`TMeta` is constrained to `notnull` so the generated OpenAPI schema marks `meta` required, and the client
reads `data.meta.x` without a null check. The meta DTO lives next to its list DTO
(`Models/Orders/OrderListMetaDto.cs`) and is documented per property — those comments become the TSDoc of the
generated type. The front end shows the result in a [`TableInfoBar`](./frontend-components.md#tableinfobar).

---

## Many aggregates over one table: `Concat` into a single `UNION ALL`

**A caller-supplied list of filters, each needing its own aggregate over the same rows, is one query, not
one per filter.** `StockStatisticsService.GetMetricCellsAsync` builds a branch per metric — the metric's
predicate, then `GroupBy(day, item)`, then a `Sum` — stamps each with a constant `MetricIndex`, and stitches
them with `Concat`. EF translates that to a single `UNION ALL` over identically-shaped subqueries, and the
index tag is what lets the rows be sorted back into per-metric slots in memory.

The alternative — a conditional `SUM(CASE WHEN … )` column per metric — needs a projection built at runtime,
because the metric count is not known at compile time; the alternative to *that* is N round trips. `Concat`
gets one statement out of plain LINQ.

Two constraints come with it. Every branch must project the same type, so the discriminator is a field of
that type rather than a separate shape. And filtering and ordering happen **before** the projection: EF
cannot translate `OrderBy` over a member of a record it has not yet materialised, which is the same reason
`StockMovementPresetService.Rows` takes an already-ordered queryable instead of ordering its own output.

---

## Table-wide invariants: `pg_advisory_xact_lock`, not a retry

**A write whose precondition is a fact about the whole table cannot be guarded by a row-level token.**
`StockMovementPresetService` has two: "exactly one preset is the default" and "at least one preset exists".
Both are read-then-write over every row, and the partial unique index on `IsDefault` is checked per
statement, so two transactions moving the default collide on it however they order their updates.

The fix is a transaction-scoped advisory lock taken as the first statement inside the unit of work — the
write still goes through `ExecuteInTransactionAsync`, so it keeps its `db.transaction` span, and
`SELECT pg_advisory_xact_lock($key)` is simply what it runs first. That serialises the writers and makes the
check-then-act safe. The lock is
released by the commit or the rollback, so unlike `PostgresAdvisoryLock` — session-scoped, on its own
connection because Npgsql's `DISCARD ALL` would drop it — it can ride on the request's own DbContext
connection. Reach for it when the invariant spans rows and the table is small and human-written; a retry loop
around the unique violation would work too, but it leaves the failure mode live and the lock does not.

Per-row lost updates are a different problem and still need the row token: the preset's `xmin` is what makes
an edit started minutes ago fail instead of overwriting someone else's save.

---

## Entity locks: `[LocksEntity<T>]`, `[Transactional]`, `IEntityLockService`

Every mutation of a document (order, receipt, write-off, stocktake), a marketplace account, card or warehouse,
or an organization runs inside one transaction that first takes a pessimistic lock on the object
it changes. The lock is held until the commit, so the checks an action makes — status, remaining quantity,
"is it already assigned" — hold against state no other writer can change until the write lands.

**On the controller** the lock is an attribute, not code in the action:

```csharp
[PublishesEntityChanged(AppEntityType.Order)]
[LocksEntity<Order>]
[HttpPost("{id:guid}/assembly-tasks/{taskId:guid}/boxes/{tbid:guid}/components/{cid:guid}/fulfillments")]
public async Task<IActionResult> AddFulfillment(Guid id, ...)
```

- `[LocksEntity<T>(routeKey = "id")]` opens the transaction and row-locks `T` by the route value **before the
  action runs**, so the action's own loading reads the locked, current row. A child route locks its aggregate
  root: anything under `/orders/{id}/…` locks the order, never the box, task or component — the aggregate's
  invariants (an order turning `Assembled` when its last task is done, a move that rewrites the order's own
  composition) are facts about the whole order.
- `[Transactional]` is the same filter without the lock, for actions whose targets come from the body. The
  action locks them itself as its first database statement:
  `await locks.LockManyAsync<Order>(request.OrderIds, ct);`. A batch addressed by child ids resolves the
  distinct parent ids with one light query, locks them, and only then loads.
- The transaction **commits only on a 2xx result**. Any other result, or an exception, rolls back everything
  the action wrote — a request rejected halfway leaves nothing behind. An action that must persist something
  *and* answer 4xx (a stored failure record) is not wrapped.
- An endpoint deleting the entity itself sets `[LocksEntity<T>(ForDelete = true)]` — see `FOR UPDATE` below.
- Filter order: `[PublishesEntityChanged]` sits outside and runs after the commit. `[PublishesAssemblyChanged]`
  sits inside (`TransactionalAttribute.FilterOrder + 1`): its "before" snapshot of the worklist is read under the
  lock, so no other commit can land between the snapshot and the action, and its event goes through the outbox
  below. Its reads run on a savepoint: a failed one costs only the event, never the request's transaction. A second transactional filter on one action throws. Inner `ExecuteInTransactionAsync` calls become
  savepoints of the request's transaction.
- A create needs no lock — the new row has nobody to race with — unless it changes an existing object too, in
  which case it locks that one. A transfer is such a create: it makes a new document and moves stock, which the
  counter rows below already protect, so `TransfersController` takes no entity lock.

**`IEntityLockService`** is what both filters call, and what services use directly:

| Method | SQL | Use for |
|--------|-----|---------|
| `LockAsync<T>(id)` | `SELECT 1 … WHERE "Id" = ANY(@ids) ORDER BY "Id" FOR NO KEY UPDATE` | an existing row |
| `LockAsync<T>(id, forDelete: true)` | same, `FOR UPDATE` | the row about to be deleted |
| `LockManyAsync<T>(ids)` | same, many ids | batches |
| `LockKeyAsync(scope, key)` | `pg_advisory_xact_lock(hash(scope:key))` | a row that does not exist yet, or a logical resource spanning rows |
| `LockKeysAsync(scope, keys)` | same over `unnest` of the sorted hashes, one statement | many such keys |

- **Row locks by default.** The database enforces a row lock against every writer — any `UPDATE` of that row
  waits, including code that never heard of the lock. An advisory lock binds only the code that remembers to
  take it. Row locks also live in the row itself, while advisory locks fill the shared lock table
  (`max_locks_per_transaction × max_connections`), which a batch over a thousand orders could exhaust.
- **`FOR NO KEY UPDATE` for changes, `FOR UPDATE` for deletes.** Inserting or relinking a child row takes
  `FOR KEY SHARE` on its parent for the foreign-key check. `FOR NO KEY UPDATE` does not conflict with it, so
  adding a box never waits on an unrelated edit of its order. A delete needs the opposite: its "nothing refers
  to it" check must hold until the commit, and only `FOR UPDATE` makes a concurrent child insert wait. A writer
  that links to an existing parent it found by lookup — `OrganizationService.LinkByInnAsync` — locks the parent
  first and reads it again, so a parent deleted meanwhile reads as absent instead of failing on the key.
- **Rows are locked in id order**, so two batches over overlapping sets cannot deadlock each other.
- Every method throws outside a transaction — a lock there would be released by the very statement that took it.
- The table and key column come from the EF model; the entity needs a single `Guid` primary key.

**A check that reads other documents** is not covered by the document's own lock — two stocktakes each hold
their own row and both pass "this cell is not counted elsewhere". Such a check locks what the documents share
before reading it: `StocktakeService.FindNodeCountedElsewhereAsync` row-locks the cells, and
`StocktakesController.SyncNodeItems` takes `LockKeysAsync("stocktake-unit", numbers)` over the claimed
serials — advisory keys, because a surplus serial may have no inventory row to lock. A batch Start takes the
cells of every stocktake in the batch in one sorted pass before the first document starts, so two batches over
crossing cells cannot lock them in opposite orders.

**Contention** waits rather than failing at once: the lock call sets `lock_timeout`
(`IEntityLockService.DefaultTimeout`, 5 s) for its own statement only and resets it once the lock is granted —
a later wait inside the action, such as a stock row behind another writer, keeps the server default. A lock not
granted in that time raises `EntityLockedException`
(`IExpectedFailure`), which the filter turns into `409 entityLocked` — nothing was written, and the request
can be repeated. Two users acting on one order at once are serialised for the few milliseconds the first
request takes and never see the error.

**Inside the transaction** a failed `SaveChanges` stays recoverable — EF wraps it in a savepoint, which is what
lets `InventoryService` replay a unique-violation conflict and `OrganizationService.LinkByInnAsync` catch a
duplicate INN. A batch holds the stock rows of every item it has written until its single commit, so two
batches touching the same groups in different orders can deadlock. The savepoint undoes only the victim's last
save while the rows it wrote earlier stay locked, so a replay would close the same cycle again: the victim's item
fails at once with `inventoryWriteConflict`, the rest of its batch carries on, and the other batch proceeds. A failed raw statement (`ExecuteSql…`, `ExecuteUpdate`, `FromSql`) has no such savepoint and
aborts the whole transaction: catching it and carrying on is not an option inside a locked action.

**Realtime events wait for the commit.** A watcher told about a change before the commit refetches the old state
and is never told again. While a filter's transaction is open, `IRealtimeNotifier` puts every event into the
request's `RealtimeOutbox`; the filter flushes it after the commit and drops it on rollback. Publishing code
calls the notifier directly and does not need to know whether a transaction is open.

**Background sync** takes the same locks in short transactions instead of holding them across a run:

- Order import locks the orders of one page, reads them, applies and commits. The status catch-up asks the
  marketplace first, so no lock is held across an external call, then applies the answers in chunks of 200 orders,
  each re-read under its lock with the original "still open" filters.
  The external-history import locks only when it refreshes known orders — without a refresh it never writes them.
- The card import locks one page of cards before reading them: auto-map writes only cards it reads as unmapped,
  and an operator's mapping saved meanwhile must not be overwritten by a stale read.
- Seller info reloads the account under its row lock before writing it and linking its organization by INN.
- Sync waits longer (`IEntityLockService.BackgroundTimeout`, 30 s). A lock still not granted fails the run with
  `entityLocked`; the next scheduled run catches up.

Columns only sync writes — `LastSync*`, a warehouse's marketplace fields — are not locked: EF updates only the
columns that changed, so a user's concurrent edit of other columns is not lost.

---

## Counter rows: unique index + `xmin` + replay

A row whose value is read, changed in C# and written back (`Count` on `StoragePlaceNodeItemsGroup`) cannot be
left unguarded. EF writes the absolute value — `SET "Count" = 15`, not `"Count" = "Count" + 5` — so two requests
that read `10` concurrently both store `15` and one increment disappears, while both journal rows survive and the
stock silently stops matching the movements.

Three parts make such a counter safe:

- **A unique index on the identity of the counter** (`(StoragePlaceNodeId, CatalogItemId)`), so a race to create
  the row fails with `23505` instead of leaving two rows that split the same stock.
- **`xmin` as a concurrency token** — `e.Property<uint>("Version").IsRowVersion()` in `OnModelCreating`. Npgsql
  maps a `uint` row-version onto the PostgreSQL `xmin` system column, so no column is added and the migration
  must not generate an `AddColumn` for it. Every `UPDATE` then carries `WHERE xmin = @original` and a lost update
  raises `DbUpdateConcurrencyException`.
- **Replaying the attempt**, not just retrying the save: `InventoryService.SaveGroupChangeAsync` re-reads the row,
  reapplies the delta and saves again, up to a small retry limit. On conflict a row that was `Added` is detached
  (someone else inserted it) and a `Modified` one is `Reload`ed, which resets both original and current values.

The caller's span is passed in: every successful save tags it with `inventory.group_write.attempts`, and each
conflict adds an `inventory.group_write.conflict` event, so contention is visible in traces instead of hiding
behind a slightly slower request.

The journal row is queued **once**, after the delegate has accepted the change, and stays pending across
attempts: `SaveChanges` is atomic, so a failed attempt leaves it `Added` and the save that finally succeeds
writes it. Queuing it per attempt would insert a row per attempt; queuing it before the delegate runs would
leave an orphan behind whenever the delegate rejects the change — and the scope is shared with callers such as
`OrdersController`'s mass fulfillment, which catches `InsufficientInventoryException` and keeps going on the
same context, so that orphan would be written by the next unrelated save.

Contention that outlives the retry budget is a **business outcome, not a server error**: nothing was written, so
the operation is safe to repeat. `SaveGroupChangeAsync` wraps the last conflict in `InventoryWriteConflictException`,
and every controller that drives stock turns it into `409 inventoryWriteConflict` — in the batch fulfillment loop
into a per-item failure, so one contended item does not abort the rest of the batch. Letting the raw
`DbUpdateException` escape would have produced a 500 and told the user nothing.

Conflict detection is narrow on purpose: a unique violation counts only when `ConstraintName` is the group's own
index. Anything else that lands in the same save is a real failure and must not be replayed away.

The same narrowness applies wherever a unique violation is turned into a business answer. `UniqueViolations`
recognises one index by name — `IsUnitInventoryNumber` matches the partial index behind "this inventory number is
already taken", and both the receipt placement and the stocktake surplus path filter on it before converting the
failure into a `ValidationException`. A bare `catch (DbUpdateException)` would report an unrelated foreign-key
violation as a duplicate number, which is a lie the caller cannot act on.

The same shape applies to any aggregate counter that several requests can touch at once. A row that is only ever
replaced wholesale, or written from a single serialized worker, does not need it.

---

## Ambient state for `IHttpClientFactory` handlers

A `DelegatingHandler` cannot read a **scoped** service written by the caller: `IHttpClientFactory` builds and caches
handler chains in its own DI scope, so the handler gets a different instance than the one the caller wrote to.

When a single `HttpClient` serves several tenants — so credentials cannot live on `DefaultRequestHeaders` — carry
them in an **`AsyncLocal`** exposed by a singleton (`Integrations/Ozon/MarketplaceRequestContext.cs`), and open a
scope around the call:

```csharp
using var _ = requestContext.Use(credentials);
await client.PingAsync(ct);
```

The ambient value flows into the handler regardless of DI scoping. `Use` returns an `IDisposable` that restores the
previous value rather than clearing it, so nested calls behave.

**The scope does not survive a `yield return`.** An `AsyncLocal` write propagates *down* through awaits but never
back *up* to the caller, and an async iterator hands control back at every yield: the consumer's execution context
is restored, and the next `MoveNextAsync` resumes the body without re-running the assignment. Opening the scope at
the top of an `async IAsyncEnumerable` therefore covers the first page only — every later page reaches the handler
with nothing in scope. Step the enumerator manually and re-enter the scope around each move:

```csharp
var pages = client.GetCardsAsync(ct).GetAsyncEnumerator(ct);
while (true)
{
    bool hasNext;
    {
        using var _ = requestContext.Use(credentials);
        hasNext = await pages.MoveNextAsync();
    }
    if (!hasNext) yield break;
    yield return pages.Current;
}
```

Plain `async` methods that page in a loop are unaffected — the whole loop runs under one execution context.

---

## Enums: pinned values, free ordering

Every enum in `Domain/`, `Models/` and `Infrastructure/` declares its numeric values explicitly:

```csharp
public enum ReceiptStatus
{
    Draft = 0,
    Planned = 1,
    Processing = 2,
    Finished = 3,
    Canceled = 4,
}
```

### Why

Enums are serialized as camelCase strings by MVC (`JsonStringEnumConverter` in `Program.cs`), but stored
as `int` — by EF in entity columns, and by the Npgsql serializer inside `jsonb` payloads such as
`MarketplaceSyncRun.Error` or `MarketplaceAccount.LastSyncError`. With implicit values, position *is* the
stored value, so a new member had to be appended at the end even when it belonged in the middle.

### Rules

- New members take the next free number, **not** the next position. Declare them where they belong logically.
- **Never renumber an existing member** — that silently reinterprets every row already stored.
- Reordering member *declarations* is free and has no effect on data. Order for readability.
- `[Flags]` enums keep powers of two; new flags take the next unused bit.

---

## Access rules: one predicate per entity type

Whether a user may see or edit an object is answered in exactly one place — an `EntityAccessRule<T>` registered in
`Infrastructure/Access/EntityAccessRegistry`. Controllers never read `permission` claims and never load the
assigned-warehouse set themselves.

### Pattern

The rule owns a predicate; callers apply it at whichever level they need:

```csharp
private EntityAccessRule<Writeoff> Rule => access.For<Writeoff>();

// list — prelude answers 403/401, the query answers "which rows"
if (AccessError(await Rule.PrecheckAsync(User, AccessLevel.View, ct)) is { } error)
    return error;
var accessible = await Rule.QueryAsync(User, AccessLevel.View, ct);

// single object — load first, then judge
if (AccessError(await Rule.CheckAsync(User, AccessLevel.Edit, writeoff, ct)) is { } denied)
    return denied;

// create — the object does not exist yet, only the warehouse from the request does
if (AccessError(await Rule.CheckWarehouseAsync(User, AccessLevel.Edit, request.WarehouseId, ct)) is { } denied)
    return denied;
```

`AccessError` (on `AppControllerBase`) turns an `AccessVerdict` into the right response — `401` for an unusable
token, `403` with the entity's own `*NotAssignedToWarehouse` code otherwise — or `null` when access is granted.

### Rules

- `PrecheckAsync` before loading, `CheckAsync` after. Skipping the prelude turns a 403 into an empty list; skipping
  the post-load check leaks objects from other warehouses.
- A list endpoint starts from `QueryAsync` and appends its own `Include`/`Where`. Never re-apply an
  `assignedIds.Contains(...)` filter on top — the rule already did it.
- The "no access" branch returns `Where(_ => false)`, not `Take(0)`, so callers can still chain `Include`.
- Action permissions (`orders.self_assign`, `receipts.process_assigned`, `transfers.*`) are not access rules —
  they authorise an operation, not a view of an object, and stay in the controller.

See [permissions.md](permissions.md#where-access-is-checked) for the layer table and how to register a new rule.
