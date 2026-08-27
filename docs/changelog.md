# Changelog System

Tracks every mutation of key entities (create / update / delete) with a before/after diff, a snapshot, and the identity of the user who made the change.

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

**Every written entry also publishes a realtime `entityChanged`** to the object's watchers, addressed past the
author so nobody is told their own save made the screen stale. The publication sits here rather than in each
controller for three reasons: the method already takes `(entityType, entityId)` — the exact key watchers are
addressed by — it already knows the acting user, and it does not write when the comparison found nothing, so a
save that changed nothing raises no event either. Entities without a changelog service are not covered; orders
publish the same event from a `[PublishesEntityChanged]` action filter instead. See
[realtime-specification.md](realtime-specification.md).

## Domain Model Notes

`ChangeLogEntry.Snapshot` and `Context` are asymmetric on purpose: `Snapshot` holds the *before* state for `Modified`/`Deleted` but the *after* state for `Added` (there is no before), while `Context` holds the *after* state and is only filled for `Modified`.

`AppEntityType.ChangeLog` and `.InventoryItem` exist only so the storage statistics page can name their tables — nothing ever writes an entry with them. Not every enum value is a tracked entity.

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

Real action constants live in `Infrastructure/*Actions.cs`, one class per feature: `ReceiptActions`, `WriteoffActions`, `TransferActions`, `OrderActions`, `MarketplaceActions`, `StocktakeActions` (`nodes_synced`, `items_synced`, `scheduled`, `moved_to_draft`, `started`, `reverted`, `finished`, `canceled`), `ForecastActions` (`forecast.override_set`, `forecast.override_cleared`) — plus `InventoryActions`, which are not changelog actions at all: they are the `StockMovement.Action` values written by `InventoryService` and therefore carry an `inventory.` prefix. A stocktake writes document-level changelog entries under `AppEntityType.Stocktake`; its per-node stock corrections land in the movement journal with `inventory.stocktake_surplus` / `_shortage` / `_relocation`.

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
