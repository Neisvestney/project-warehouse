using AutoMapper;
using Microsoft.EntityFrameworkCore;
using ProjectWarehouse.Server.Data;
using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Infrastructure;
using ProjectWarehouse.Server.Infrastructure.ChangeLog;
using ProjectWarehouse.Server.Models.Receipts;
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
            ?? throw new InvalidOperationException(
                $"Storage place node '{nodeId}' was not found. Cannot snapshot inventory state.");

        return mapper.Map<StoragePlaceNodeDetailsDto>(node);
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
            throw new InvalidOperationException(
                $"Cannot remove {count} item(s) of catalog item '{catalogItemId}' from node '{nodeId}': insufficient inventory (available: {group?.Count ?? 0}).");

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
        string action = InventoryActions.RemoveUnitItem,
        CancellationToken ct = default)
    {
        var item = await db.InventoryItems.OfType<UnitInventoryItem>()
            .FirstOrDefaultAsync(u => u.Id == unitItemId, ct)
            ?? throw new InvalidOperationException(
                $"UnitInventoryItem '{unitItemId}' was not found.");

        var nodeId = item.StoragePlaceNodeId;
        var before = await SnapshotNodeAsync(nodeId, ct);

        db.InventoryItems.Remove(item);
        await db.SaveChangesAsync(ct);

        var after = await SnapshotNodeAsync(nodeId, ct);
        await changeLog.CompareAndSaveToChangelog(before, after, action);
    }

    // ── Assembled bundle items ────────────────────────────────────────────────

    public async Task<AssembledBundleInventoryItem> AddAssembledBundleToNodeAsync(
        Guid nodeId,
        Guid catalogItemId,
        IReadOnlyList<AssembledBundlePlacementComponentRequest> components,
        string action = InventoryActions.AddAssembledBundle,
        CancellationToken ct = default)
    {
        var before = await SnapshotNodeAsync(nodeId, ct);

        // For components with NewUnitItem, create the unit item first (validates uniqueness, adds to context).
        var bundleComponents = new List<AssembledBundleInventoryItemComponent>(components.Count);
        for (var i = 0; i < components.Count; i++)
        {
            var c = components[i];
            if (c.NewUnitItem is not null)
            {
                UnitInventoryItem unitItem;
                try
                {
                    unitItem = await CreateUnitItemAsync(nodeId, c.CatalogItemId, c.NewUnitItem.InventoryNumber, ct);
                }
                catch (ValidationException ex)
                {
                    throw ex.WithPrefix($"components[{i}]");
                }
                bundleComponents.Add(new AssembledBundleInventoryItemComponent
                {
                    Id                  = Guid.NewGuid(),
                    UnitInventoryItemId = unitItem.Id,
                });
            }
            else
            {
                bundleComponents.Add(new AssembledBundleInventoryItemComponent
                {
                    Id                  = Guid.NewGuid(),
                    UnitInventoryItemId = c.UnitInventoryItemId,
                    CatalogItemId       = c.UnitInventoryItemId is null ? c.CatalogItemId : null,
                    Quantity            = c.UnitInventoryItemId is null ? c.Quantity : null,
                });
            }
        }

        var bundle = new AssembledBundleInventoryItem
        {
            Id                 = Guid.NewGuid(),
            StoragePlaceNodeId = nodeId,
            CatalogItemId      = catalogItemId,
            Components         = bundleComponents,
        };
        db.InventoryItems.Add(bundle);
        await db.SaveChangesAsync(ct);

        var after = await SnapshotNodeAsync(nodeId, ct);
        await changeLog.CompareAndSaveToChangelog(before, after, action);

        return bundle;
    }

    public async Task RemoveAssembledBundleAsync(
        Guid assembledBundleItemId,
        string action = InventoryActions.RemoveAssembledBundle,
        CancellationToken ct = default)
    {
        var item = await db.InventoryItems.OfType<AssembledBundleInventoryItem>()
            .FirstOrDefaultAsync(ab => ab.Id == assembledBundleItemId, ct)
            ?? throw new InvalidOperationException(
                $"AssembledBundleInventoryItem '{assembledBundleItemId}' was not found.");

        var nodeId = item.StoragePlaceNodeId;
        var before = await SnapshotNodeAsync(nodeId, ct);

        db.InventoryItems.Remove(item);
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
        CancellationToken ct = default)
    {
        await RemoveStandardItemsFromNodeAsync(fromNodeId, catalogItemId, count,
            InventoryActions.MoveStandardItems, ct);
        await AddStandardItemsToNodeAsync(toNodeId, catalogItemId, count,
            InventoryActions.MoveStandardItems, ct);
    }

    public async Task MoveUnitItemAsync(
        Guid unitItemId,
        Guid toNodeId,
        CancellationToken ct = default)
    {
        var item = await db.InventoryItems.OfType<UnitInventoryItem>()
            .FirstOrDefaultAsync(u => u.Id == unitItemId, ct)
            ?? throw new InvalidOperationException(
                $"UnitInventoryItem '{unitItemId}' was not found.");

        var fromNodeId = item.StoragePlaceNodeId;

        var fromBefore = await SnapshotNodeAsync(fromNodeId, ct);
        var toBefore   = await SnapshotNodeAsync(toNodeId, ct);

        item.StoragePlaceNodeId = toNodeId;
        await db.SaveChangesAsync(ct);

        var fromAfter = await SnapshotNodeAsync(fromNodeId, ct);
        var toAfter   = await SnapshotNodeAsync(toNodeId, ct);

        await changeLog.CompareAndSaveToChangelog(fromBefore, fromAfter, InventoryActions.MoveUnitItem);
        await changeLog.CompareAndSaveToChangelog(toBefore, toAfter, InventoryActions.MoveUnitItem);
    }
}
