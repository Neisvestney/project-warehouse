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

### Rules

- Always use `?? ""` on nullable string fields inside `SearchString` to avoid null propagation in SQL.
- Non-nullable string fields don't strictly need `?? ""`, but it's kept for consistency.
- `WhereMatchesSearch` with a `null` or whitespace `searchString` is a no-op — no filter is applied.
- Token splitting is space-based; each token must appear somewhere in the concatenated string (AND across tokens).

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

Introduced by the marketplaces module — the first place in the project that does work outside a request.
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

## File attachments: real FKs + GC driven by model metadata

Files live in `DataFiles` and are referenced **only through real foreign keys** — a `Guid? XFileId` column for
1:1, a join entity for 1:N. A file identifier must never be parked in `jsonb`, a string column, or an array
without a FK: the collector sees foreign keys and nothing else, and would delete such a file as an orphan.

### Why FKs and not a polymorphic attachment table

A `EntityFileAttachment(EntityType, EntityId, DataFileId)` table modelled on `ChangeLogEntry` was rejected: its
`EntityId` cannot be a foreign key, so there is no referential integrity, and the collection is not an EF
navigation — meaning it does not map through `ProjectTo` and needs a separate query and endpoint on every page.
The FK variant costs one migration per new attachment point and breaks no existing pattern.

### `OnDelete` rules

| Direction | Behaviour | Why |
|-----------|-----------|-----|
| Join entity → owner (`CatalogItemImage` → `CatalogItem`) | `Cascade` | Ordinary child collection |
| Any reference → `DataFile` | **`Restrict`, always** | `Cascade` would mean "deleting a file deletes the product". `SetNull` would quietly detach it. `Restrict` lets the database refuse to drop a file still in use — a second line under the GC |
| `DataFile` → `ApplicationUser` | `SetNull` | Audit reference, as everywhere else |

### Attaching from a controller

Controllers never write this logic themselves — they call `IDataFileBindingService`:

```csharp
var problem =
    await fileBinding.BindSingleAsync(request.MainImageFileId,
        v => item.MainImageFileId = v, "mainImageFileId", ct)
    ?? await fileBinding.BindListAsync(request.Images, item.Images, db.CatalogItemImages,
        setOwner: img => img.CatalogItemId = item.Id, field: "images", ct);
if (problem is not null) return Problem(problem);
```

The join entity implements `IDataFileLink` and the request element `IDataFileLinkRequest`, so the service stays
independent of the concrete types; internally it validates existence and then syncs through the identity-based
`IListUpdater` overload. It needs an AutoMapper map from the request to the link entity with `Id` ignored.

**Rules**
- Existence is checked explicitly, never left to the foreign key: a raw `23503` surfaces as a 500, while the
  frontend can only render an `AppProblemDetails`.
- Never inline the check-and-sync in a controller. Copying it is how one endpoint eventually ships without the
  check.
- Removing a link does not delete the file — the collector does, later.

### Garbage collection

A file is garbage when **no foreign key points at its row**. That single condition covers both "uploaded but the
form was never saved" and "the reference was removed later", so no controller has to call anything on save or
delete — there is no such call to forget.

`DataFileReferences.GetReferencingColumns(IModel)` derives the list of referencing table/column pairs from the EF
model at runtime, so registering a new attachment point *is* adding the foreign key. `Distinct()` is mandatory:
TPH maps several entity types onto one table (`InventoryItem`) and would otherwise duplicate conditions.
Identifiers are double-quoted — they come from the model, not from user input, but Postgres folds unquoted names
to lower case and this schema is PascalCase.

`DataFilesGcJob` runs on Quartz, takes a **PostgreSQL advisory lock** (the job store is in-memory, so
`[DisallowConcurrentExecution]` only covers one process), and issues a single
`DELETE ... WHERE ... NOT EXISTS (...) LIMIT @batch RETURNING "StorageKey", "SizeBytes"`.

**Order matters: the row first, the bytes second.** The reverse order could leave a row pointing at a file that
no longer exists — a broken image in the UI. This way the worst case is unused bytes on disk.

`OrphanTtlHours` protects freshly uploaded files and is therefore also the **hard deadline on an open form**:
a tab left open longer than that will fail its save with `dataFileNotFound`. Do not drop it below a day.

The same predicate builder backs the storage statistics endpoint, so the page and the collector can never
disagree about what an orphan is.

### Inheriting a file reference

`CatalogItem.MainImage` inherits from the parent group the way `EffectiveDescription` does — but it is **not** a
`[Projectable]`. Every `[Projectable]` in this project returns a `string` or a `bool`; one returning a navigation
would coalesce two entity references, and whether EF folds member access over that into `CASE WHEN` is untested
here. Worse, the same expression run in memory by `mapper.Map` silently yields null unless both navigations are
`Include`d — a failure invisible until a screenshot looks wrong.

Instead the mapper holds one shared expression that builds the DTO on **both** branches. Branches returning a
non-entity type translate unambiguously, and the expression stays correct in memory.

**A list of files is not inherited.** `s.Images.Any() ? s.Images : s.Group.Images` is a conditional over a
collection, which EF cannot translate, and the workaround breaks on recursion into children. It is also worse
semantically: "child has no images, so show all of the group's" makes "this variant deliberately has only a main
photo" unsayable.

The pair `mainImageFileId` (own) + `mainImage` (effective) is what lets the UI label an inherited image. Loading
the effective value into an edit form as the item's own would silently make it own on the next save — read the
own field when populating a form.

**Attaching images to an aggregate that already has several collections needs `AsSplitQuery()`.** EF's default
single-query mode `JOIN`s every `Include`d collection together, so the row count is their *product*.
`CatalogController.LoadItemWithDetailsAsync` pulls nine — tags, bundle components, both variation sides, images,
marketplace cards and group children with their own tags and images — and a group of 20 children with 5 images
each multiplies out into six figures of duplicated rows for one item. Split query (also used in
`OrdersController`) issues one statement per collection instead.

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
