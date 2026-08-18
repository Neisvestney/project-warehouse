# Changelog System

Tracks every mutation of key entities (create / update / delete) with a before/after diff, a snapshot, and the identity of the user who made the change.

## File Map

```
Domain/
  ChangeLogEntry.cs          — EF entity stored in DB
  ChangeLogDiff.cs           — one field diff (path, from, to)
  AppEntityType.cs           — enum of tracked entity types (append-only: persisted as int in ChangeLogEntry.EntityType)

Infrastructure/ChangeLog/
  AbstractChangeLogService.cs           — base logic: diffing, serialization, interfaces
  AppChangeLogService.cs                — concrete EF implementation (writes to DB)
  UserDetailDtoChangelogService.cs      — typed wrapper for UserDetailDto
  CatalogItemDtoChangelogService.cs     — typed wrapper for CatalogItemDto
  WarehouseDtoChangelogService.cs       — typed wrapper for WarehouseDto
  StoragePlaceNodeDetailsDtoChangelogService.cs
  RolesListDtoChangelogService.cs       — tracks the whole roles list as one object
  StocktakeDtoChangelogService.cs       — typed wrapper for StocktakeDto

Models/ChangeLog/
  ChangeLogEntryDto.cs       — API response shape

Infrastructure/
  IHasIdentity.cs            — interface for objects with a stable Guid Id (entities and DTOs)
```

## How It Works

1. **Before a mutation** — capture a DTO snapshot of the current state (`before`).
2. **After saving** — capture the new state (`after`).
3. Call `CompareAndSaveToChangelog(before, after)` on the typed service.
4. The base service diffs the two objects with `CompareNetObjects`, serializes diffs + snapshots as `jsonb`, and saves a `ChangeLogEntry` row.

| `before` | `after` | Entry type |
|----------|---------|------------|
| `null`   | object  | `Added`    |
| object   | `null`  | `Deleted`  |
| object   | object  | `Modified` (skipped if nothing changed) |

The diff is skipped entirely if `before == after` — no unnecessary rows.

## Domain Model

```csharp
ChangeLogEntry {
    Guid            Id
    AppEntityType   EntityType          // which kind of entity
    Guid            EntityId            // primary key of the entity
    ChangeLogEntryType ChangeLogEntryType  // Added / Modified / Deleted
    IList<ChangeLogDiff> Diffs          // per-field diffs (jsonb)
    string?         Snapshot            // full serialized state (before for Modified/Deleted, after for Added)
    string?         Context             // full serialized state after for Modified
    Guid?           UserId              // null if user was deleted (SetNull FK)
    DateTime        CreatedAt
    string?         Action              // optional: machine-readable reason for the change
    string?         ActionData          // optional: structured context for that reason (jsonb)
}

// AppEntityType.ChangeLog and .InventoryItem exist only so the storage statistics page can name
// their tables; nothing writes an entry with them. Not every value is a tracked entity.

ChangeLogDiff {
    string   Path   // dotted property path, e.g. "ItemsGroups[0].Count"
    object?  From
    object?  To
}
```

## Action and ActionData

Most mutations are triggered directly by a user in the UI — in those cases `Action` and `ActionData` stay `null`.

Some mutations happen in a business-process context where **why** the change happened carries meaning beyond who pressed Save. Use `Action` (a short machine-readable key) and `ActionData` (arbitrary JSON payload) to record that context.

```csharp
await changeLog.CompareAndSaveToChangelog(
    before, after,
    action: "receiving",
    actionData: new { DocumentNumber = "ПРХ-00042", TerminalId = "TSD-07" }
);
```

Examples:

| Scenario | `Action` | `ActionData` |
|----------|----------|--------------|
| Employee scans items on TSD during goods receiving | `"receiving"` | `{ documentNumber, terminalId }` |
| Inventory count corrects item quantities | `"inventory"` | `{ inventoryId, warehouseId }` |
| Bulk import from external system | `"import"` | `{ sourceSystem, fileName }` |

This allows the UI to render a human-friendly explanation ("Changed during receiving ПРХ-00042") instead of a generic "Modified by user".

Real action constants live in `Infrastructure/*Actions.cs`, one class per feature: `ReceiptActions`, `WriteoffActions`, `TransferActions`, `OrderActions`, `MarketplaceActions`, `StocktakeActions` (`nodes_synced`, `items_synced`, `scheduled`, `moved_to_draft`, `started`, `reverted`, `finished`, `canceled`) — plus `InventoryActions`, whose values double as `StockMovement.Action` and therefore carry an `inventory.` prefix. A stocktake writes document-level entries under `AppEntityType.Stocktake` and, through `InventoryService`, per-node entries under `AppEntityType.StoragePlaceNode` with `inventory.stocktake_surplus` / `_shortage` / `_relocation`.

## Adding Changelog to a New Method

### 1. Pick the right DTO type

Use the richest read DTO for that entity (the one returned by the GET-by-id endpoint), so the snapshot is as complete as possible.

### 2. Ensure the typed service exists

Check `Infrastructure/ChangeLog/` for a `{DtoType}ChangelogService`. If it doesn't exist, create one following this pattern:

```csharp
public class MyDtoChangelogService(IChangeLogService changeLogService) : IChangeLogService<MyDto>
{
    private const AppEntityType EntityType = AppEntityType.MyEntity;

    public Task CompareAndSaveToChangelog(MyDto? before, MyDto? after, string? action = null, object? actionData = null)
    {
        var logic = AbstractChangeLogService.GetCompareLogic();
        return changeLogService.CompareAndSaveToChangelog(
            EntityType, before?.Id ?? after?.Id ?? Guid.Empty,
            before, after, logic, action, actionData);
    }

    public IQueryable<ChangeLogEntry> GetChangelog(Guid entityId) =>
        changeLogService.GetChangelog(EntityType, entityId);
}
```

Then register it in `Program.cs`:

```csharp
builder.Services.AddScoped<IChangeLogService<MyDto>, MyDtoChangelogService>();
```

### 3. Implement IHasIdentity on the DTO

The DTO **must** implement `IHasIdentity` (or the `Id` property must exist) so `CollectionMatchingSpec` can match nested collection items by identity during diff.

```csharp
public class MyDto : IHasIdentity
{
    public Guid Id { get; set; }
    // ...
}
```

### 4. Inject and call in the controller

```csharp
public class MyController(
    ApplicationDbContext db,
    IMapper mapper,
    IChangeLogService<MyDto> changeLog) : AppControllerBase
```

**Create:**
```csharp
// after SaveChangesAsync
await changeLog.CompareAndSaveToChangelog(null, mapper.Map<MyDto>(entity));
```

**Update:**
```csharp
// before any mutation
var beforeDto = mapper.Map<MyDto>(entity); // entity must have nav props loaded

// after SaveChangesAsync
await changeLog.CompareAndSaveToChangelog(beforeDto, mapper.Map<MyDto>(entity));
```

**Delete:**
```csharp
// before Remove + SaveChangesAsync — use ProjectTo or load full Include chain
var beforeDto = await db.MyEntities
    .ProjectTo<MyDto>(mapper.ConfigurationProvider)
    .FirstOrDefaultAsync(x => x.Id == id, ct);

db.MyEntities.Remove(entity);
await db.SaveChangesAsync(ct);

await changeLog.CompareAndSaveToChangelog(beforeDto, null);
```

### 5. Load all navigation properties before mapping

`mapper.Map<T>(entity)` is in-memory — EF will **not** lazy-load missing nav props. Before taking a `before` snapshot, ensure the entity is loaded with the full Include chain that the DTO mapping needs:

```csharp
var entity = await db.MyEntities
    .Include(e => e.Children)
        .ThenInclude(c => c.SubChildren)
    .FirstOrDefaultAsync(e => e.Id == id, ct);
```

Alternatively, use `ProjectTo<MyDto>` (EF translates it to SQL), which avoids the Include problem entirely and is preferred for read-only snapshots.

## Querying the Changelog

**Base service** — per entity:
```csharp
IQueryable<ChangeLogEntry> entries = changeLogService.GetChangelog(AppEntityType.Warehouse, warehouseId);
```

**REST API** — global paginated list with optional filters:
```
GET /api/changelog?page=1&pageSize=20&entityType=warehouse&changeLogEntryType=modified
```

Requires permission: `changelog.view`.
