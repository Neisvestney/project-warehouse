using Microsoft.EntityFrameworkCore;
using ProjectWarehouse.Server.Data;
using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Infrastructure;
using ProjectWarehouse.Server.Models.Stocktakes;

namespace ProjectWarehouse.Server.Services;

public class StocktakeDiffCalculator(ApplicationDbContext db) : IStocktakeDiffCalculator
{
    public async Task<StocktakePlan> BuildPlanAsync(Stocktake stocktake, CancellationToken ct = default)
    {
        var nodeIds = stocktake.Nodes.Select(n => n.StoragePlaceNodeId).ToList();

        var groups = await db.StoragePlacesNodesItemsGroups
            .Where(g => nodeIds.Contains(g.StoragePlaceNodeId) && g.Count > 0)
            .Select(g => new { g.StoragePlaceNodeId, g.CatalogItemId, g.Count })
            .ToListAsync(ct);
        var expectedStandard = groups.ToDictionary(g => (g.StoragePlaceNodeId, g.CatalogItemId), g => g.Count);

        var liveUnits = await db.InventoryItems.OfType<UnitInventoryItem>()
            .Where(u => u.StoragePlaceNodeId != null && nodeIds.Contains(u.StoragePlaceNodeId!.Value))
            .Select(u => new UnitSnapshot(u.Id, u.InventoryNumber, u.CatalogItemId, u.StoragePlaceNodeId))
            .ToListAsync(ct);

        // Serials the document names may live anywhere (or nowhere yet), so they are resolved separately
        var claimedNumbers = stocktake.Nodes
            .SelectMany(n => n.Items)
            .Where(i => i.Kind == StocktakeItemKind.Unit && i.InventoryNumber != null)
            .Select(i => i.InventoryNumber!)
            .Distinct()
            .ToList();

        var referencedUnits = claimedNumbers.Count == 0
            ? []
            : await db.InventoryItems.OfType<UnitInventoryItem>()
                .Where(u => claimedNumbers.Contains(u.InventoryNumber))
                .Select(u => new UnitSnapshot(u.Id, u.InventoryNumber, u.CatalogItemId, u.StoragePlaceNodeId))
                .ToListAsync(ct);

        var unitsByKey = referencedUnits
            .Concat(liveUnits)
            .DistinctBy(u => u.Id)
            .ToDictionary(u => (u.CatalogItemId, u.InventoryNumber));

        var heldByAssembly = await LoadUnitsHeldByAssemblyAsync(
            referencedUnits.Where(u => u.NodeId is null).Select(u => u.Id).ToList(), ct);

        var (nodePaths, warehouseByNode) = await LoadNodeContextAsync(
            nodeIds, referencedUnits.Where(u => u.NodeId is not null).Select(u => u.NodeId!.Value), ct);

        var names = await LoadCatalogNamesAsync(stocktake, liveUnits, groups.Select(g => g.CatalogItemId), ct);

        // A serial claimed found anywhere in the document must not be detached by the cell that lost it
        var claimedFound = stocktake.Nodes
            .SelectMany(n => n.Items)
            .Where(i => i.Kind == StocktakeItemKind.Unit && i.CountedQuantity > 0)
            .Select(i => ResolveUnit(i, unitsByKey)?.Id)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .ToHashSet();

        var lines = new List<StocktakePlanLine>();
        var problems = new List<StocktakePlanProblem>();

        foreach (var node in stocktake.Nodes)
        {
            var nodeId = node.StoragePlaceNodeId;
            var warehouseId = warehouseByNode.GetValueOrDefault(nodeId);

            BuildStandardLines(node, nodeId, expectedStandard, names, lines);
            BuildUnitLines(node, nodeId, warehouseId, unitsByKey, heldByAssembly, warehouseByNode, claimedFound, names,
                lines, problems);
            BuildUncountedUnitLines(node, nodeId, liveUnits, claimedFound, names, lines);
        }

        return new StocktakePlan { Lines = lines, Problems = problems, NodePaths = nodePaths };
    }

    private static void BuildStandardLines(
        StocktakeNode node,
        Guid nodeId,
        Dictionary<(Guid, Guid), int> expectedStandard,
        Dictionary<Guid, string> names,
        List<StocktakePlanLine> lines)
    {
        var counted = new HashSet<Guid>();

        foreach (var item in node.Items.Where(i => i.Kind == StocktakeItemKind.Standard))
        {
            counted.Add(item.CatalogItemId);
            var expected = expectedStandard.GetValueOrDefault((nodeId, item.CatalogItemId));
            lines.Add(StandardLine(nodeId, item.Id, item.CatalogItemId, names, expected, item.CountedQuantity));
        }

        // Stock the document says nothing about: the cell is authoritative, so it counts as zero
        foreach (var ((groupNodeId, catalogItemId), expected) in expectedStandard)
        {
            if (groupNodeId != nodeId || counted.Contains(catalogItemId)) continue;
            lines.Add(StandardLine(nodeId, null, catalogItemId, names, expected, counted: 0));
        }
    }

    private static StocktakePlanLine StandardLine(
        Guid nodeId,
        Guid? itemId,
        Guid catalogItemId,
        Dictionary<Guid, string> names,
        int expected,
        int counted)
    {
        var delta = counted - expected;
        return new StocktakePlanLine
        {
            StoragePlaceNodeId = nodeId,
            StocktakeItemId = itemId,
            Kind = StocktakeItemKind.Standard,
            CatalogItemId = catalogItemId,
            CatalogItemName = names.GetValueOrDefault(catalogItemId, ""),
            Expected = expected,
            Counted = counted,
            Delta = delta,
            Resolution = delta switch
            {
                > 0 => StocktakeDifferenceResolution.Surplus,
                < 0 => StocktakeDifferenceResolution.Shortage,
                _ => StocktakeDifferenceResolution.NoChange,
            },
        };
    }

    private static void BuildUnitLines(
        StocktakeNode node,
        Guid nodeId,
        Guid warehouseId,
        Dictionary<(Guid, string), UnitSnapshot> unitsByKey,
        HashSet<Guid> heldByAssembly,
        Dictionary<Guid, Guid> warehouseByNode,
        HashSet<Guid> claimedFound,
        Dictionary<Guid, string> names,
        List<StocktakePlanLine> lines,
        List<StocktakePlanProblem> problems)
    {
        foreach (var item in node.Items.Where(i => i.Kind == StocktakeItemKind.Unit))
        {
            var unit = ResolveUnit(item, unitsByKey);
            var found = item.CountedQuantity > 0;

            var resolution = StocktakeDifferenceResolution.NoChange;
            var delta = 0;
            Guid? currentNodeId = null;

            if (!found)
            {
                // A serial found in another cell of this document is relocated, not detached —
                // otherwise the cell that lost it would undo the move
                if (unit?.NodeId == nodeId && !claimedFound.Contains(unit.Id))
                {
                    resolution = StocktakeDifferenceResolution.DetachUnit;
                    delta = -1;
                }
            }
            else if (unit is null)
            {
                resolution = StocktakeDifferenceResolution.CreateUnit;
                delta = 1;
            }
            else if (unit.NodeId == nodeId)
            {
                resolution = StocktakeDifferenceResolution.NoChange;
            }
            else if (unit.NodeId is null)
            {
                if (heldByAssembly.Contains(unit.Id))
                {
                    problems.Add(new StocktakePlanProblem
                    {
                        StoragePlaceNodeId = nodeId,
                        StocktakeItemId = item.Id,
                        Code = ErrorCode.StocktakeUnitItemDetached,
                        Message = $"Экземпляр «{item.InventoryNumber}» удерживается сборкой заказа.",
                    });
                }
                else
                {
                    resolution = StocktakeDifferenceResolution.ReattachUnit;
                    delta = 1;
                }
            }
            else
            {
                currentNodeId = unit.NodeId;
                if (warehouseByNode.GetValueOrDefault(unit.NodeId.Value) != warehouseId)
                {
                    problems.Add(new StocktakePlanProblem
                    {
                        StoragePlaceNodeId = nodeId,
                        StocktakeItemId = item.Id,
                        Code = ErrorCode.StocktakeUnitItemInAnotherWarehouse,
                        Message = $"Экземпляр «{item.InventoryNumber}» числится на другом складе.",
                    });
                }
                else
                {
                    resolution = StocktakeDifferenceResolution.Relocation;
                    delta = 1;
                }
            }

            lines.Add(new StocktakePlanLine
            {
                StoragePlaceNodeId = nodeId,
                StocktakeItemId = item.Id,
                Kind = StocktakeItemKind.Unit,
                CatalogItemId = item.CatalogItemId,
                CatalogItemName = names.GetValueOrDefault(item.CatalogItemId, ""),
                InventoryNumber = item.InventoryNumber,
                UnitInventoryItemId = unit?.Id,
                Expected = unit?.NodeId == nodeId ? 1 : 0,
                Counted = found ? 1 : 0,
                Delta = delta,
                Resolution = resolution,
                CurrentNodeId = currentNodeId,
            });
        }
    }

    private static void BuildUncountedUnitLines(
        StocktakeNode node,
        Guid nodeId,
        List<UnitSnapshot> liveUnits,
        HashSet<Guid> claimedFound,
        Dictionary<Guid, string> names,
        List<StocktakePlanLine> lines)
    {
        var mentioned = node.Items
            .Where(i => i.Kind == StocktakeItemKind.Unit && i.InventoryNumber != null)
            .Select(i => (i.CatalogItemId, i.InventoryNumber!))
            .ToHashSet();

        foreach (var unit in liveUnits.Where(u => u.NodeId == nodeId))
        {
            if (mentioned.Contains((unit.CatalogItemId, unit.InventoryNumber))) continue;
            if (claimedFound.Contains(unit.Id)) continue;

            lines.Add(new StocktakePlanLine
            {
                StoragePlaceNodeId = nodeId,
                StocktakeItemId = null,
                Kind = StocktakeItemKind.Unit,
                CatalogItemId = unit.CatalogItemId,
                CatalogItemName = names.GetValueOrDefault(unit.CatalogItemId, ""),
                InventoryNumber = unit.InventoryNumber,
                UnitInventoryItemId = unit.Id,
                Expected = 1,
                Counted = 0,
                Delta = -1,
                Resolution = StocktakeDifferenceResolution.DetachUnit,
            });
        }
    }

    private static UnitSnapshot? ResolveUnit(
        StocktakeItem item,
        Dictionary<(Guid, string), UnitSnapshot> unitsByKey) =>
        item.InventoryNumber is null
            ? null
            : unitsByKey.GetValueOrDefault((item.CatalogItemId, item.InventoryNumber));

    private async Task<HashSet<Guid>> LoadUnitsHeldByAssemblyAsync(List<Guid> unitIds, CancellationToken ct)
    {
        if (unitIds.Count == 0) return [];

        var direct = await db.AssemblyFulfillments
            .Where(f => f.UnitInventoryItemId != null && unitIds.Contains(f.UnitInventoryItemId!.Value))
            .Select(f => f.UnitInventoryItemId!.Value)
            .ToListAsync(ct);

        var bundled = await db.AssemblyFulfillmentBundleComponents
            .Where(c => c.UnitInventoryItemId != null && unitIds.Contains(c.UnitInventoryItemId!.Value))
            .Select(c => c.UnitInventoryItemId!.Value)
            .ToListAsync(ct);

        return [.. direct, .. bundled];
    }

    private async Task<(Dictionary<Guid, string[]> Paths, Dictionary<Guid, Guid> WarehouseByNode)>
        LoadNodeContextAsync(List<Guid> scopeNodeIds, IEnumerable<Guid> extraNodeIds, CancellationToken ct)
    {
        var allIds = scopeNodeIds.Concat(extraNodeIds).Distinct().ToList();

        var warehouseIds = await db.StoragePlacesNodes
            .Where(n => allIds.Contains(n.Id))
            .Select(n => new { n.Id, n.RootStoragePlace.WarehouseId })
            .ToListAsync(ct);
        var warehouseByNode = warehouseIds.ToDictionary(x => x.Id, x => x.WarehouseId);

        // Paths need every node of the involved warehouses to walk the parent chain
        var involvedWarehouses = warehouseByNode.Values.Distinct().ToList();
        var nodes = await db.StoragePlacesNodes
            .Include(n => n.RootStoragePlace)
            .Where(n => involvedWarehouses.Contains(n.RootStoragePlace.WarehouseId))
            .ToListAsync(ct);
        var nodeById = nodes.ToDictionary(n => n.Id);

        var paths = allIds
            .Where(nodeById.ContainsKey)
            .ToDictionary(id => id, id => StoragePlaceNodeHelper.BuildPath(nodeById[id], nodeById));

        return (paths, warehouseByNode);
    }

    private async Task<Dictionary<Guid, string>> LoadCatalogNamesAsync(
        Stocktake stocktake,
        List<UnitSnapshot> liveUnits,
        IEnumerable<Guid> groupItemIds,
        CancellationToken ct)
    {
        var ids = stocktake.Nodes.SelectMany(n => n.Items).Select(i => i.CatalogItemId)
            .Concat(liveUnits.Select(u => u.CatalogItemId))
            .Concat(groupItemIds)
            .Distinct()
            .ToList();

        if (ids.Count == 0) return [];

        return await db.CatalogItems
            .Where(c => ids.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Name, ct);
    }

    public StocktakeDifferencesDto ToDto(StocktakePlan plan)
    {
        var nodes = plan.Lines
            .GroupBy(l => l.StoragePlaceNodeId)
            .Select(g => new StocktakeNodeDifferencesDto
            {
                StoragePlaceNodeId = g.Key,
                NodePath = plan.NodePaths.GetValueOrDefault(g.Key, []),
                Lines = [.. g.Select(l => new StocktakeDifferenceLineDto
                {
                    Kind = l.Kind,
                    CatalogItemId = l.CatalogItemId,
                    CatalogItemName = l.CatalogItemName,
                    InventoryNumber = l.InventoryNumber,
                    Expected = l.Expected,
                    Counted = l.Counted,
                    Delta = l.Delta,
                    Resolution = l.Resolution,
                    MissingFromDocument = l.MissingFromDocument,
                    CurrentNodeId = l.CurrentNodeId,
                    CurrentNodePath = l.CurrentNodeId is { } id ? plan.NodePaths.GetValueOrDefault(id) : null,
                })],
            })
            .ToList();

        return new StocktakeDifferencesDto
        {
            Nodes = nodes,
            TotalSurplusQuantity = plan.Lines.Where(l => l.Delta > 0).Sum(l => l.Delta),
            TotalShortageQuantity = plan.Lines.Where(l => l.Delta < 0).Sum(l => -l.Delta),
            TotalRelocations = plan.Lines.Count(l => l.Resolution == StocktakeDifferenceResolution.Relocation),
            HasDifferences = plan.Lines.Any(l => l.Resolution != StocktakeDifferenceResolution.NoChange),
            Problems = [.. plan.Problems.Select(p => new StocktakeProblemDto
            {
                StoragePlaceNodeId = p.StoragePlaceNodeId,
                Code = p.Code,
                Message = p.Message,
            })],
        };
    }

    private record UnitSnapshot(Guid Id, string InventoryNumber, Guid CatalogItemId, Guid? NodeId);
}
