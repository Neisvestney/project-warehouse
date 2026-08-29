# Backend Patterns

Recurring implementation patterns used in the server project.

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
    EF.Functions.ILike(SearchString, pattern, SearchExtensions.EscapeChar)
    || Boxes.Any(b => EF.Functions.ILike(b.Label ?? "", pattern, SearchExtensions.EscapeChar))
    || Boxes.Any(b => b.Components.Any(c =>
        EF.Functions.ILike(c.CatalogItem.SearchString, pattern, SearchExtensions.EscapeChar)));
```

**Controller** (`OrdersController.cs`):
```csharp
query.WhereMatchesExtendedSearch((o, pattern) => o.MatchesExtendedSearch(pattern), searchString)
```

`WhereMatchesExtendedSearch` keeps the same contract as `WhereMatchesSearch` — it owns tokenization and `%`, `_`,
`\` escaping, and substitutes the finished pattern into the predicate once per token. Semantics stay **AND across
tokens, OR across sources**. Nested `SearchString` properties on related entities expand normally inside it.

Cost: one correlated `EXISTS` per collection per token, with no index behind `ILIKE`. Fine for small or paginated
sets; reach for `pg_trgm` or a materialized column before pointing it at a large table.

### Rules

- Always use `?? ""` on nullable string fields inside `SearchString` to avoid null propagation in SQL.
- Non-nullable string fields don't strictly need `?? ""`, but it's kept for consistency.
- `WhereMatchesSearch` with a `null` or whitespace `searchString` is a no-op — so is `WhereMatchesExtendedSearch`.
- Token splitting is space-based; each token must appear somewhere in the concatenated string (AND across tokens).
- Scalar fields → `SearchString` + `WhereMatchesSearch`. Anything reached through a collection →
  `MatchesXxxSearch(pattern)` + `WhereMatchesExtendedSearch`. Never `string.Join` over a navigation.
- Both live in `SearchExtensions`; escaping is `SearchExtensions.EscapeChar`, never a bare `"\\"` literal.

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

## Several `Include`d collections need `AsSplitQuery()`

**Attaching images to an aggregate that already has several collections needs `AsSplitQuery()`.** EF's default
single-query mode `JOIN`s every `Include`d collection together, so the row count is their *product*.
`CatalogController.LoadItemWithDetailsAsync` pulls nine — tags, bundle components, both variation sides, images,
marketplace cards and group children with their own tags and images — and a group of 20 children with 5 images
each multiplies out into six figures of duplicated rows for one item. Split query (also used in
`OrdersController`) issues one statement per collection instead.

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
