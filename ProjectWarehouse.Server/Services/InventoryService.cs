using AutoMapper;
using Microsoft.EntityFrameworkCore;
using ProjectWarehouse.Server.Data;
using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Infrastructure;
using ProjectWarehouse.Server.Infrastructure.ChangeLog;
using ProjectWarehouse.Server.Models.Warehouses;

namespace ProjectWarehouse.Server.Services;

public class InventoryService(
    ApplicationDbContext db,
    IMapper mapper,
    IChangeLogService<StoragePlaceNodeDetailsDto> changeLog) : IInventoryService
{
    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Loads a node with its items for changelog diffing.
    /// Throws <see cref="InvalidOperationException"/> with a clear message if the node does not exist.
    /// </summary>
    private async Task<StoragePlaceNodeDetailsDto> SnapshotNodeAsync(Guid nodeId, CancellationToken ct)
    {
        var node = await db.StoragePlacesNodes
            .Include(n => n.ItemsGroups).ThenInclude(g => g.CatalogItem)
            .Include(n => n.InventoryItems)
            .FirstOrDefaultAsync(n => n.Id == nodeId, ct)
            ?? throw new StoragePlaceNodeNotFoundException(nodeId);

        return mapper.Map<StoragePlaceNodeDetailsDto>(node);
    }

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

    // ── Standard items ────────────────────────────────────────────────────────

    public async Task AddStandardItemsToNodeAsync(
        Guid nodeId,
        Guid catalogItemId,
        int count,
        string action = InventoryActions.AddStandardItems,
        CancellationToken ct = default)
    {
        if (count <= 0)
            throw new ArgumentOutOfRangeException(nameof(count), count,
                "Count must be greater than zero.");

        var before = await SnapshotNodeAsync(nodeId, ct);

        var group = await db.StoragePlacesNodesItemsGroups
            .FirstOrDefaultAsync(g => g.StoragePlaceNodeId == nodeId && g.CatalogItemId == catalogItemId, ct);

        if (group is null)
        {
            group = new StoragePlaceNodeItemsGroup
            {
                Id = Guid.NewGuid(),
                StoragePlaceNodeId = nodeId,
                CatalogItemId = catalogItemId,
                Count = 0,
            };
            db.StoragePlacesNodesItemsGroups.Add(group);
        }

        group.Count += count;
        await db.SaveChangesAsync(ct);

        var after = await SnapshotNodeAsync(nodeId, ct);
        await changeLog.CompareAndSaveToChangelog(before, after, action);
    }

    public async Task RemoveStandardItemsFromNodeAsync(
        Guid nodeId,
        Guid catalogItemId,
        int count,
        string action = InventoryActions.RemoveStandardItems,
        CancellationToken ct = default)
    {
        if (count <= 0)
            throw new ArgumentOutOfRangeException(nameof(count), count,
                "Count must be greater than zero.");

        var before = await SnapshotNodeAsync(nodeId, ct);

        var group = await db.StoragePlacesNodesItemsGroups
            .FirstOrDefaultAsync(g => g.StoragePlaceNodeId == nodeId && g.CatalogItemId == catalogItemId, ct);

        if (group is null || group.Count < count)
            throw await BuildInsufficientInventoryExceptionAsync(nodeId, catalogItemId, group?.Count ?? 0, count, ct);

        group.Count -= count;
        await db.SaveChangesAsync(ct);

        var after = await SnapshotNodeAsync(nodeId, ct);
        await changeLog.CompareAndSaveToChangelog(before, after, action);
    }

    // ── Unit items ────────────────────────────────────────────────────────────

    public async Task<UnitInventoryItem> CreateUnitItemAsync(
        Guid nodeId,
        Guid catalogItemId,
        string inventoryNumber,
        CancellationToken ct = default)
    {
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
        return item;
    }

    public async Task<UnitInventoryItem> PlaceUnitItemToNodeAsync(
        Guid nodeId,
        Guid catalogItemId,
        string inventoryNumber,
        string action = InventoryActions.AddUnitItem,
        CancellationToken ct = default)
    {
        var before = await SnapshotNodeAsync(nodeId, ct);

        var item = await CreateUnitItemAsync(nodeId, catalogItemId, inventoryNumber, ct);
        await db.SaveChangesAsync(ct);

        var after = await SnapshotNodeAsync(nodeId, ct);
        await changeLog.CompareAndSaveToChangelog(before, after, action);

        return item;
    }

    public async Task RemoveUnitItemAsync(
        Guid unitItemId,
        Guid expectedNodeId,
        string action = InventoryActions.RemoveUnitItem,
        CancellationToken ct = default)
    {
        var item = await db.InventoryItems.OfType<UnitInventoryItem>()
            .FirstOrDefaultAsync(u => u.Id == unitItemId, ct)
            ?? throw new UnitInventoryItemNotFoundException(unitItemId);

        if (item.StoragePlaceNodeId != expectedNodeId)
            throw new InventoryItemNodeMismatchException(unitItemId, expectedNodeId, item.StoragePlaceNodeId ?? Guid.Empty);

        var nodeId = item.StoragePlaceNodeId!.Value;
        var before = await SnapshotNodeAsync(nodeId, ct);

        db.InventoryItems.Remove(item);
        await db.SaveChangesAsync(ct);

        var after = await SnapshotNodeAsync(nodeId, ct);
        await changeLog.CompareAndSaveToChangelog(before, after, action);
    }

    public async Task DetachUnitItemAsync(
        Guid unitItemId,
        Guid expectedNodeId,
        string action = InventoryActions.RemoveUnitItem,
        CancellationToken ct = default)
    {
        var item = await db.InventoryItems.OfType<UnitInventoryItem>()
            .FirstOrDefaultAsync(u => u.Id == unitItemId, ct)
            ?? throw new UnitInventoryItemNotFoundException(unitItemId);

        if (item.StoragePlaceNodeId != expectedNodeId)
            throw new InventoryItemNodeMismatchException(unitItemId, expectedNodeId, item.StoragePlaceNodeId ?? Guid.Empty);

        var nodeId = expectedNodeId;
        var before = await SnapshotNodeAsync(nodeId, ct);

        item.StoragePlaceNodeId = null;
        await db.SaveChangesAsync(ct);

        var after = await SnapshotNodeAsync(nodeId, ct);
        await changeLog.CompareAndSaveToChangelog(before, after, action);
    }

    public async Task ReattachUnitItemAsync(
        Guid unitItemId,
        Guid nodeId,
        string action = InventoryActions.AddUnitItem,
        CancellationToken ct = default)
    {
        var item = await db.InventoryItems.OfType<UnitInventoryItem>()
            .FirstOrDefaultAsync(u => u.Id == unitItemId, ct)
            ?? throw new UnitInventoryItemNotFoundException(unitItemId);

        if (item.StoragePlaceNodeId != null)
            throw new InventoryItemNodeMismatchException(unitItemId, nodeId, item.StoragePlaceNodeId.Value);

        var before = await SnapshotNodeAsync(nodeId, ct);

        item.StoragePlaceNodeId = nodeId;
        await db.SaveChangesAsync(ct);

        var after = await SnapshotNodeAsync(nodeId, ct);
        await changeLog.CompareAndSaveToChangelog(before, after, action);
    }

    // ── Movement ──────────────────────────────────────────────────────────────

    public async Task MoveStandardItemsAsync(
        Guid fromNodeId,
        Guid toNodeId,
        Guid catalogItemId,
        int count,
        string action = InventoryActions.MoveStandardItems,
        CancellationToken ct = default)
    {
        await RemoveStandardItemsFromNodeAsync(fromNodeId, catalogItemId, count, action, ct);
        await AddStandardItemsToNodeAsync(toNodeId, catalogItemId, count, action, ct);
    }

    public async Task MoveUnitItemAsync(
        Guid unitItemId,
        Guid toNodeId,
        string action = InventoryActions.MoveUnitItem,
        CancellationToken ct = default)
    {
        var item = await db.InventoryItems.OfType<UnitInventoryItem>()
            .FirstOrDefaultAsync(u => u.Id == unitItemId, ct)
            ?? throw new UnitInventoryItemNotFoundException(unitItemId);

        var fromNodeId = item.StoragePlaceNodeId
            ?? throw new InvalidOperationException($"Unit inventory item {unitItemId} is detached and cannot be moved.");

        var fromBefore = await SnapshotNodeAsync(fromNodeId, ct);
        var toBefore   = await SnapshotNodeAsync(toNodeId, ct);

        item.StoragePlaceNodeId = toNodeId;
        await db.SaveChangesAsync(ct);

        var fromAfter = await SnapshotNodeAsync(fromNodeId, ct);
        var toAfter   = await SnapshotNodeAsync(toNodeId, ct);

        await changeLog.CompareAndSaveToChangelog(fromBefore, fromAfter, action);
        await changeLog.CompareAndSaveToChangelog(toBefore, toAfter, action);
    }
}
