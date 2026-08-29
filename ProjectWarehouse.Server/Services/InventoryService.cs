using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using ProjectWarehouse.Server.Data;
using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Infrastructure;
using ProjectWarehouse.Server.Infrastructure.Observability;

namespace ProjectWarehouse.Server.Services;

public class InventoryService(
    ApplicationDbContext db,
    IHttpContextAccessor httpContextAccessor) : IInventoryService
{
    // ── Helpers ──────────────────────────────────────────────────────────────

    /// Scoped service, so this lives for one request — batch endpoints hitting several shortages
    /// in a row reuse the warehouse node map instead of re-reading it per failure.
    private readonly Dictionary<Guid, Dictionary<Guid, StoragePlaceNode>> _nodesByWarehouse = [];

    /// <summary>
    /// Resolves the item name and the node breadcrumb so the error can be shown to the user.
    /// Only runs on the failure path, so the extra queries are not on the hot path.
    /// </summary>
    private async Task<InsufficientInventoryException> BuildInsufficientInventoryExceptionAsync(
        Guid nodeId, Guid catalogItemId, int available, int requested, CancellationToken ct)
    {
        var itemName = await db.CatalogItems
            .Where(i => i.Id == catalogItemId)
            .Select(i => i.FullName)
            .FirstOrDefaultAsync(ct) ?? "";

        var node = await db.StoragePlacesNodes
            .Include(n => n.RootStoragePlace)
            .FirstOrDefaultAsync(n => n.Id == nodeId, ct);

        string[]? path = null;
        if (node is not null)
            path = StoragePlaceNodeHelper.BuildPath(
                node, await GetWarehouseNodesAsync(node.RootStoragePlace.WarehouseId, ct));

        return new InsufficientInventoryException(nodeId, catalogItemId, available, requested, itemName, path);
    }

    private async Task<Dictionary<Guid, StoragePlaceNode>> GetWarehouseNodesAsync(
        Guid warehouseId, CancellationToken ct)
    {
        if (_nodesByWarehouse.TryGetValue(warehouseId, out var cached)) return cached;

        var nodeById = await db.StoragePlacesNodes
            .Where(n => n.RootStoragePlace.WarehouseId == warehouseId)
            .Include(n => n.RootStoragePlace)
            .ToDictionaryAsync(n => n.Id, ct);

        _nodesByWarehouse[warehouseId] = nodeById;
        return nodeById;
    }

    // ── Stock movement journal ────────────────────────────────────────────────

    private readonly Dictionary<Guid, (Guid StoragePlaceId, Guid WarehouseId)> _locationByNode = [];

    private async Task<(Guid StoragePlaceId, Guid WarehouseId)?> GetNodeLocationAsync(Guid nodeId, CancellationToken ct)
    {
        if (_locationByNode.TryGetValue(nodeId, out var cached)) return cached;

        var location = await db.StoragePlacesNodes
            .Where(n => n.Id == nodeId)
            .Select(n => new { n.RootStoragePlaceId, n.RootStoragePlace.WarehouseId })
            .FirstOrDefaultAsync(ct);

        if (location is null) return null;

        var resolved = (location.RootStoragePlaceId, location.WarehouseId);
        _locationByNode[nodeId] = resolved;
        return resolved;
    }

    /// <summary>
    /// Queues a journal row for the current stock change. Added to the context but not saved — the
    /// caller saves it together with the change itself, so a movement can never exist without it.
    /// Returns the id of the queued row so the caller can put it on its span.
    /// </summary>
    private async Task<Guid> RecordMovementAsync(
        Guid nodeId,
        Guid catalogItemId,
        int quantity,
        StockMovementDirection direction,
        string action,
        CancellationToken ct,
        UnitInventoryItem? unitItem = null) =>
        (await QueueMovementAsync(nodeId, catalogItemId, quantity, direction, action, ct, unitItem)).Id;

    private async Task<StockMovement> QueueMovementAsync(
        Guid nodeId,
        Guid catalogItemId,
        int quantity,
        StockMovementDirection direction,
        string action,
        CancellationToken ct,
        UnitInventoryItem? unitItem = null)
    {
        var location = await GetNodeLocationAsync(nodeId, ct)
            ?? throw new StoragePlaceNodeNotFoundException(nodeId);

        var movement = new StockMovement
        {
            Id                  = Guid.NewGuid(),
            CreatedAt           = DateTime.UtcNow,
            Direction           = direction,
            Action              = action,
            Quantity            = quantity,
            CatalogItemId       = catalogItemId,
            StoragePlaceNodeId  = nodeId,
            StoragePlaceId      = location.StoragePlaceId,
            WarehouseId         = location.WarehouseId,
            UnitInventoryItemId = unitItem?.Id,
            UnitInventoryNumber = unitItem?.InventoryNumber,
            UserId              = GetCurrentUserId(),
        };

        db.StockMovements.Add(movement);
        return movement;
    }

    /// <summary>
    /// Same as <see cref="RecordMovementAsync"/> for a single unit item, putting both the item id and its
    /// number on the row. The id is an audit reference nulled out when the item is deleted; the number is
    /// a copy and stays, so a movement of a piece that no longer exists is still identifiable.
    /// </summary>
    private Task<Guid> RecordUnitMovementAsync(
        Guid nodeId,
        UnitInventoryItem item,
        StockMovementDirection direction,
        string action,
        CancellationToken ct) =>
        RecordMovementAsync(nodeId, item.CatalogItemId, 1, direction, action, ct, item);

    private Guid? GetCurrentUserId() =>
        Guid.TryParse(httpContextAccessor.HttpContext?.User.FindFirstValue(JwtRegisteredClaimNames.Sub), out var id)
            ? id
            : null;

    private const int GroupWriteRetryLimit = 3;

    private const string GroupUniqueIndexName = "IX_ItemsGroup_StoragePlaceNodeId_CatalogItemId";

    /// A unique violation is only ours if it names the group's index — anything else queued into the same
    /// save is a genuine failure and must not be swallowed by a replay.
    private static bool IsGroupWriteConflict(Exception e) =>
        e is DbUpdateConcurrencyException
        || (e is DbUpdateException { InnerException: PostgresException pg }
            && pg.SqlState == PostgresErrorCodes.UniqueViolation
            && pg.ConstraintName == GroupUniqueIndexName);

    /// <summary>
    /// Resolves the group for the node/item pair, lets <paramref name="apply"/> mutate it and saves.
    /// Count is a read-modify-write, so a concurrent writer either bumps xmin under us or wins the race
    /// to insert the group; both surface as a conflict here and the whole attempt is replayed against
    /// freshly loaded state. The journal row is queued only once <paramref name="apply"/> has accepted the
    /// change and then stays pending across attempts, so a rejected change leaves nothing behind in the
    /// context — the scope is shared with callers that keep going after a business failure. Contention that
    /// outlives the retry budget becomes an <see cref="InventoryWriteConflictException"/>: nothing was
    /// written, so the caller can report it as retryable rather than as a server error.
    /// </summary>
    private async Task<Guid> SaveGroupChangeAsync(
        Guid nodeId,
        Guid catalogItemId,
        Func<StoragePlaceNodeItemsGroup?, Task<StoragePlaceNodeItemsGroup>> apply,
        Func<Task<StockMovement>> queueMovement,
        Activity? activity,
        CancellationToken ct)
    {
        StockMovement? movement = null;
        StoragePlaceNodeItemsGroup? applied = null;

        try
        {
            for (var attempt = 0; ; attempt++)
            {
                var group = await db.StoragePlacesNodesItemsGroups
                    .FirstOrDefaultAsync(g => g.StoragePlaceNodeId == nodeId && g.CatalogItemId == catalogItemId, ct);

                applied = await apply(group);
                if (db.Entry(applied).State == EntityState.Detached)
                    db.StoragePlacesNodesItemsGroups.Add(applied);

                movement ??= await queueMovement();

                try
                {
                    await db.SaveChangesAsync(ct);
                    activity?.SetTag("inventory.group_write.attempts", attempt + 1);
                    return movement.Id;
                }
                catch (Exception e) when (IsGroupWriteConflict(e))
                {
                    activity?.AddEvent(new ActivityEvent("inventory.group_write.conflict", tags: new ActivityTagsCollection
                    {
                        ["inventory.group_write.attempt"] = attempt + 1,
                        ["exception.type"] = e.GetType().Name,
                    }));

                    if (attempt >= GroupWriteRetryLimit)
                        throw new InventoryWriteConflictException(nodeId, catalogItemId, attempt + 1, e);

                    var entry = db.Entry(applied);
                    if (entry.State == EntityState.Added)
                        entry.State = EntityState.Detached;
                    else
                        // A row deleted meanwhile reloads as Detached rather than throwing, so the next
                        // pass reads null: Add recreates the group, Remove reports it as out of stock.
                        await entry.ReloadAsync(ct);
                }
            }
        }
        catch
        {
            // Leaving on any other route means nothing was committed, so drop what the attempt queued:
            // the scope is shared with callers that carry on after a failure and whose next save would
            // otherwise flush these rows as part of an unrelated change.
            if (movement is not null)
                db.Entry(movement).State = EntityState.Detached;
            if (applied is not null)
                db.Entry(applied).State = EntityState.Detached;
            throw;
        }
    }

    // ── Standard items ────────────────────────────────────────────────────────

    public Task AddStandardItemsToNodeAsync(
        Guid nodeId,
        Guid catalogItemId,
        int count,
        string action = InventoryActions.UnknownAction,
        CancellationToken ct = default) =>
        AddStandardCoreAsync(nodeId, catalogItemId, count, action, StockMovementDirection.In, ct);

    private async Task AddStandardCoreAsync(
        Guid nodeId,
        Guid catalogItemId,
        int count,
        string action,
        StockMovementDirection direction,
        CancellationToken ct)
    {
        using var activity = AppTelemetry.Source.StartActivity("inventory.standard.add");
        activity?.SetTag("inventory.node_id", nodeId);
        activity?.SetTag("inventory.catalog_item_id", catalogItemId);
        activity?.SetTag("inventory.count", count);
        activity?.SetTag("inventory.action", action);
        activity?.SetTag("inventory.direction", direction.ToString());

        if (count <= 0)
            throw new ArgumentOutOfRangeException(nameof(count), count,
                "Count must be greater than zero.");

        var movementId = await SaveGroupChangeAsync(nodeId, catalogItemId, group =>
        {
            group ??= new StoragePlaceNodeItemsGroup
            {
                Id = Guid.NewGuid(),
                StoragePlaceNodeId = nodeId,
                CatalogItemId = catalogItemId,
                Count = 0,
            };

            group.Count += count;
            return Task.FromResult(group);
        }, () => QueueMovementAsync(nodeId, catalogItemId, count, direction, action, ct), activity, ct);

        activity?.SetTag("inventory.stock_movement_id", movementId);
    }

    public Task RemoveStandardItemsFromNodeAsync(
        Guid nodeId,
        Guid catalogItemId,
        int count,
        string action = InventoryActions.UnknownAction,
        CancellationToken ct = default) =>
        RemoveStandardCoreAsync(nodeId, catalogItemId, count, action, StockMovementDirection.Out, ct);

    private async Task RemoveStandardCoreAsync(
        Guid nodeId,
        Guid catalogItemId,
        int count,
        string action,
        StockMovementDirection direction,
        CancellationToken ct)
    {
        using var activity = AppTelemetry.Source.StartActivity("inventory.standard.remove");
        activity?.SetTag("inventory.node_id", nodeId);
        activity?.SetTag("inventory.catalog_item_id", catalogItemId);
        activity?.SetTag("inventory.count", count);
        activity?.SetTag("inventory.action", action);
        activity?.SetTag("inventory.direction", direction.ToString());

        if (count <= 0)
            throw new ArgumentOutOfRangeException(nameof(count), count,
                "Count must be greater than zero.");

        var movementId = await SaveGroupChangeAsync(nodeId, catalogItemId, async group =>
        {
            if (group is null || group.Count < count)
                throw await BuildInsufficientInventoryExceptionAsync(nodeId, catalogItemId, group?.Count ?? 0, count, ct);

            group.Count -= count;
            return group;
        }, () => QueueMovementAsync(nodeId, catalogItemId, count, direction, action, ct), activity, ct);

        activity?.SetTag("inventory.stock_movement_id", movementId);
    }

    // ── Unit items ────────────────────────────────────────────────────────────

    public async Task<UnitInventoryItem> CreateUnitItemAsync(
        Guid nodeId,
        Guid catalogItemId,
        string inventoryNumber,
        CancellationToken ct = default)
    {
        using var activity = AppTelemetry.Source.StartActivity("inventory.unit.create");
        activity?.SetTag("inventory.node_id", nodeId);
        activity?.SetTag("inventory.catalog_item_id", catalogItemId);
        activity?.SetTag("inventory.count", 1);

        var exists = await db.InventoryItems.OfType<UnitInventoryItem>()
            .AnyAsync(u => u.CatalogItemId == catalogItemId && u.InventoryNumber == inventoryNumber, ct);

        if (exists)
            throw new ValidationException(
                "inventoryNumber",
                ErrorCode.UnitInventoryItemNumberDuplicate,
                $"A unit inventory item with number '{inventoryNumber}' already exists for catalog item '{catalogItemId}'.");

        var item = new UnitInventoryItem
        {
            Id                 = Guid.NewGuid(),
            StoragePlaceNodeId = nodeId,
            CatalogItemId      = catalogItemId,
            InventoryNumber    = inventoryNumber,
        };
        db.InventoryItems.Add(item);
        activity?.SetTag("inventory.unit_item_id", item.Id);
        return item;
    }

    public async Task<UnitInventoryItem> PlaceUnitItemToNodeAsync(
        Guid nodeId,
        Guid catalogItemId,
        string inventoryNumber,
        string action = InventoryActions.UnknownAction,
        CancellationToken ct = default)
    {
        using var activity = AppTelemetry.Source.StartActivity("inventory.unit.place");
        activity?.SetTag("inventory.node_id", nodeId);
        activity?.SetTag("inventory.catalog_item_id", catalogItemId);
        activity?.SetTag("inventory.count", 1);
        activity?.SetTag("inventory.action", action);

        var item = await CreateUnitItemAsync(nodeId, catalogItemId, inventoryNumber, ct);
        var movementId = await RecordUnitMovementAsync(nodeId, item, StockMovementDirection.In, action, ct);
        await db.SaveChangesAsync(ct);
        activity?.SetTag("inventory.unit_item_id", item.Id);
        activity?.SetTag("inventory.stock_movement_id", movementId);

        return item;
    }

    public async Task RemoveUnitItemAsync(
        Guid unitItemId,
        Guid expectedNodeId,
        string action = InventoryActions.UnknownAction,
        CancellationToken ct = default)
    {
        using var activity = AppTelemetry.Source.StartActivity("inventory.unit.remove");
        activity?.SetTag("inventory.unit_item_id", unitItemId);
        activity?.SetTag("inventory.node_id", expectedNodeId);
        activity?.SetTag("inventory.count", 1);
        activity?.SetTag("inventory.action", action);

        var item = await db.InventoryItems.OfType<UnitInventoryItem>()
            .FirstOrDefaultAsync(u => u.Id == unitItemId, ct)
            ?? throw new UnitInventoryItemNotFoundException(unitItemId);
        activity?.SetTag("inventory.catalog_item_id", item.CatalogItemId);

        if (item.StoragePlaceNodeId != expectedNodeId)
            throw new InventoryItemNodeMismatchException(unitItemId, expectedNodeId, item.StoragePlaceNodeId ?? Guid.Empty);

        var nodeId = item.StoragePlaceNodeId!.Value;
        db.InventoryItems.Remove(item);
        var movementId = await RecordUnitMovementAsync(nodeId, item, StockMovementDirection.Out, action, ct);
        await db.SaveChangesAsync(ct);
        activity?.SetTag("inventory.stock_movement_id", movementId);
    }

    public async Task DetachUnitItemAsync(
        Guid unitItemId,
        Guid expectedNodeId,
        string action = InventoryActions.UnknownAction,
        CancellationToken ct = default)
    {
        using var activity = AppTelemetry.Source.StartActivity("inventory.unit.detach");
        activity?.SetTag("inventory.unit_item_id", unitItemId);
        activity?.SetTag("inventory.node_id", expectedNodeId);
        activity?.SetTag("inventory.count", 1);
        activity?.SetTag("inventory.action", action);

        var item = await db.InventoryItems.OfType<UnitInventoryItem>()
            .FirstOrDefaultAsync(u => u.Id == unitItemId, ct)
            ?? throw new UnitInventoryItemNotFoundException(unitItemId);
        activity?.SetTag("inventory.catalog_item_id", item.CatalogItemId);

        if (item.StoragePlaceNodeId != expectedNodeId)
            throw new InventoryItemNodeMismatchException(unitItemId, expectedNodeId, item.StoragePlaceNodeId ?? Guid.Empty);

        var nodeId = expectedNodeId;
        item.StoragePlaceNodeId = null;
        var movementId = await RecordUnitMovementAsync(nodeId, item, StockMovementDirection.Out, action, ct);
        await db.SaveChangesAsync(ct);
        activity?.SetTag("inventory.stock_movement_id", movementId);
    }

    public async Task ReattachUnitItemAsync(
        Guid unitItemId,
        Guid nodeId,
        string action = InventoryActions.UnknownAction,
        CancellationToken ct = default)
    {
        using var activity = AppTelemetry.Source.StartActivity("inventory.unit.reattach");
        activity?.SetTag("inventory.unit_item_id", unitItemId);
        activity?.SetTag("inventory.node_id", nodeId);
        activity?.SetTag("inventory.count", 1);
        activity?.SetTag("inventory.action", action);

        var item = await db.InventoryItems.OfType<UnitInventoryItem>()
            .FirstOrDefaultAsync(u => u.Id == unitItemId, ct)
            ?? throw new UnitInventoryItemNotFoundException(unitItemId);
        activity?.SetTag("inventory.catalog_item_id", item.CatalogItemId);

        if (item.StoragePlaceNodeId != null)
            throw new InventoryItemNodeMismatchException(unitItemId, nodeId, item.StoragePlaceNodeId.Value);

        item.StoragePlaceNodeId = nodeId;
        var movementId = await RecordUnitMovementAsync(nodeId, item, StockMovementDirection.In, action, ct);
        await db.SaveChangesAsync(ct);
        activity?.SetTag("inventory.stock_movement_id", movementId);
    }

    // ── Movement ──────────────────────────────────────────────────────────────

    public async Task MoveStandardItemsAsync(
        Guid fromNodeId,
        Guid toNodeId,
        Guid catalogItemId,
        int count,
        string action = InventoryActions.MoveStock,
        CancellationToken ct = default)
    {
        using var activity = AppTelemetry.Source.StartActivity("inventory.standard.move");
        activity?.SetTag("inventory.from_node_id", fromNodeId);
        activity?.SetTag("inventory.to_node_id", toNodeId);
        activity?.SetTag("inventory.catalog_item_id", catalogItemId);
        activity?.SetTag("inventory.count", count);
        activity?.SetTag("inventory.action", action);

        await RemoveStandardCoreAsync(fromNodeId, catalogItemId, count, action, StockMovementDirection.TransferOut, ct);
        await AddStandardCoreAsync(toNodeId, catalogItemId, count, action, StockMovementDirection.TransferIn, ct);
    }

    public async Task MoveUnitItemAsync(
        Guid unitItemId,
        Guid toNodeId,
        string action = InventoryActions.MoveStock,
        CancellationToken ct = default)
    {
        using var activity = AppTelemetry.Source.StartActivity("inventory.unit.move");
        activity?.SetTag("inventory.unit_item_id", unitItemId);
        activity?.SetTag("inventory.to_node_id", toNodeId);
        activity?.SetTag("inventory.count", 1);
        activity?.SetTag("inventory.action", action);

        var item = await db.InventoryItems.OfType<UnitInventoryItem>()
            .FirstOrDefaultAsync(u => u.Id == unitItemId, ct)
            ?? throw new UnitInventoryItemNotFoundException(unitItemId);
        activity?.SetTag("inventory.catalog_item_id", item.CatalogItemId);

        var fromNodeId = item.StoragePlaceNodeId
            ?? throw new InvalidOperationException($"Unit inventory item {unitItemId} is detached and cannot be moved.");

        item.StoragePlaceNodeId = toNodeId;
        var outMovementId = await RecordUnitMovementAsync(fromNodeId, item, StockMovementDirection.TransferOut, action, ct);
        var inMovementId = await RecordUnitMovementAsync(toNodeId, item, StockMovementDirection.TransferIn, action, ct);
        await db.SaveChangesAsync(ct);
        activity?.SetTag("inventory.from_node_id", fromNodeId);
        activity?.SetTag("inventory.stock_movement_out_id", outMovementId);
        activity?.SetTag("inventory.stock_movement_in_id", inMovementId);
    }

    // ── Reads ────────────────────────────────────────────────────────────────

    public async Task<Dictionary<Guid, int>> GetCurrentStockAsync(
        IReadOnlyCollection<Guid> warehouseIds,
        Guid? warehouseId,
        Guid? storagePlaceId,
        Guid? nodeId,
        IReadOnlyCollection<Guid>? catalogItemIds,
        CancellationToken ct = default)
    {
        using var activity = AppTelemetry.Source.StartActivity("inventory.stock.current");
        activity?.SetTag("inventory.warehouses.in_scope", warehouseIds.Count);
        activity?.SetTag("inventory.warehouse_id", warehouseId);
        activity?.SetTag("inventory.storage_place_id", storagePlaceId);
        activity?.SetTag("inventory.node_id", nodeId);
        activity?.SetTag("inventory.catalog_items.requested", catalogItemIds?.Count);

        var groups = db.StoragePlacesNodesItemsGroups
            .Where(g => warehouseIds.Contains(g.StoragePlaceNode.RootStoragePlace.WarehouseId))
            .Where(g => warehouseId == null || g.StoragePlaceNode.RootStoragePlace.WarehouseId == warehouseId)
            .Where(g => storagePlaceId == null || g.StoragePlaceNode.RootStoragePlaceId == storagePlaceId)
            .Where(g => nodeId == null || g.StoragePlaceNodeId == nodeId);
        if (catalogItemIds != null)
            groups = groups.Where(g => catalogItemIds.Contains(g.CatalogItemId));

        var items = db.InventoryItems
            .Where(i => i.StoragePlaceNodeId != null)
            .Where(i => warehouseIds.Contains(i.StoragePlaceNode!.RootStoragePlace.WarehouseId))
            .Where(i => warehouseId == null || i.StoragePlaceNode!.RootStoragePlace.WarehouseId == warehouseId)
            .Where(i => storagePlaceId == null || i.StoragePlaceNode!.RootStoragePlaceId == storagePlaceId)
            .Where(i => nodeId == null || i.StoragePlaceNodeId == nodeId);
        if (catalogItemIds != null)
            items = items.Where(i => catalogItemIds.Contains(i.CatalogItemId));

        var groupTotals = await groups
            .GroupBy(g => g.CatalogItemId)
            .Select(g => new { g.Key, Count = g.Sum(x => x.Count) })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);

        var itemTotals = await items
            .GroupBy(i => i.CatalogItemId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);

        var result = new Dictionary<Guid, int>(groupTotals);
        foreach (var (id, count) in itemTotals)
            result[id] = result.GetValueOrDefault(id) + count;

        activity?.SetTag("inventory.catalog_items.returned", result.Count);
        return result;
    }
}
