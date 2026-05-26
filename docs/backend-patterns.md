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

When the searchable fields span a navigation property, put `SearchString` on the related entity and navigate to it in the expression:

**Domain entity** (`CatalogItemWithCharacteristic.cs`):
```csharp
[Projectable]
public string SearchString =>
    (CatalogItem.Name ?? "") + " " +
    (CatalogItem.Article ?? "") + " " +
    (CatalogItem.Barcode ?? "") + " " +
    (Barcode ?? "") + " " +
    (Characteristic ?? "");
```

**Controller** (`WarehousesController.cs`):
```csharp
db.StoragePlacesNodesItemsGroups
    .WhereMatchesSearch(g => g.CatalogItemWithCharacteristic.SearchString, searchString)
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
