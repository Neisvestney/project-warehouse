using Microsoft.EntityFrameworkCore;
using ProjectWarehouse.Server.Data;
using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Infrastructure;
using ProjectWarehouse.Server.Models;
using ProjectWarehouse.Server.Models.Orders;

namespace ProjectWarehouse.Server.Services;

public class AssemblyCheckService(
    ApplicationDbContext db,
    ICatalogService catalog,
    IInventoryService inventory) : IAssemblyCheckService
{
    public IEnumerable<(Guid NodeId, Guid CatalogItemId, int Quantity)> EnumerateStockDemand(
        Guid componentCatalogItemId, CatalogItemType componentType, AddFulfillmentRequest fulfillment)
    {
        if (fulfillment.BundleComponents is { Count: > 0 } leaves)
        {
            foreach (var leaf in leaves.Where(l => !l.UnitInventoryItemId.HasValue))
                yield return (leaf.SourceNodeId, leaf.CatalogItemId, leaf.Quantity);

            yield break;
        }

        if (fulfillment.UnitInventoryItemId.HasValue || fulfillment.Quantity <= 0 || !fulfillment.SourceNodeId.HasValue)
            yield break;

        // Mirrors how the service resolves the item: only a Variation defers to the client's pick.
        var catalogItemId = componentType == CatalogItemType.Variation
            ? fulfillment.ResolvedCatalogItemId
            : componentCatalogItemId;

        if (catalogItemId.HasValue)
            yield return (fulfillment.SourceNodeId.Value, catalogItemId.Value, fulfillment.Quantity);
    }

    public async Task<Dictionary<(Guid NodeId, Guid CatalogItemId), int>> ReadStockAsync(
        IReadOnlyCollection<(Guid NodeId, Guid CatalogItemId)> pairs, CancellationToken ct = default)
    {
        if (pairs.Count == 0)
            return [];

        var nodeIds = pairs.Select(p => p.NodeId).Distinct().ToList();
        var itemIds = pairs.Select(p => p.CatalogItemId).Distinct().ToList();

        // Queried as a cross product and filtered in memory — the pair count here is a handful
        var groups = await db.StoragePlacesNodesItemsGroups
            .AsNoTracking()
            .Where(g => nodeIds.Contains(g.StoragePlaceNodeId) && itemIds.Contains(g.CatalogItemId))
            .Select(g => new { g.StoragePlaceNodeId, g.CatalogItemId, g.Count })
            .ToListAsync(ct);

        return groups
            .Where(g => pairs.Contains((g.StoragePlaceNodeId, g.CatalogItemId)))
            .ToDictionary(g => (g.StoragePlaceNodeId, g.CatalogItemId), g => g.Count);
    }

    public async Task<AssemblyCheckPreviewDto> PreviewAsync(
        IQueryable<Order> accessible, IReadOnlyCollection<Guid> orderIds, CancellationToken ct = default)
    {
        var orders = await accessible
            .AsNoTracking()
            .Where(o => orderIds.Contains(o.Id) && !o.IsExternal && o.WarehouseId != null
                        && (o.Status == OrderStatus.Draft || o.Status == OrderStatus.Confirmed
                            || o.Status == OrderStatus.Assembly))
            .Select(o => new
            {
                o.Id,
                o.Number,
                o.Status,
                WarehouseId = o.WarehouseId!.Value,
                PostingNumber = o.MarketplaceOrder != null ? o.MarketplaceOrder.PostingNumber : null,
            })
            .ToListAsync(ct);

        var assemblyIds = orders.Where(o => o.Status == OrderStatus.Assembly).Select(o => o.Id).ToList();
        var plannedIds = orders.Where(o => o.Status != OrderStatus.Assembly).Select(o => o.Id).ToList();

        var tasksByOrder = (await db.AssemblyTasks
                .AsNoTracking()
                .Include(t => t.AssignedTo)
                .Include(t => t.Boxes.OrderBy(b => b.Id)).ThenInclude(b => b.OrderBox)
                .Include(t => t.Boxes).ThenInclude(b => b.Components).ThenInclude(c => c.CatalogItem).ThenInclude(i => i.Group)
                .Include(t => t.Boxes).ThenInclude(b => b.Components).ThenInclude(c => c.Fulfillments).ThenInclude(f => f.BundleComponents)
                .Where(t => assemblyIds.Contains(t.OrderId))
                .OrderBy(t => t.Id)
                .AsSplitQuery()
                .ToListAsync(ct))
            .ToLookup(t => t.OrderId);

        var boxesByOrder = (await db.OrderBoxes
                .AsNoTracking()
                .Include(b => b.Components).ThenInclude(c => c.CatalogItem).ThenInclude(i => i.Group)
                .Where(b => plannedIds.Contains(b.OrderId))
                .OrderBy(b => b.Id)
                .AsSplitQuery()
                .ToListAsync(ct))
            .ToLookup(b => b.OrderId);

        var result = new List<AssemblyCheckTaskDto>();
        foreach (var order in orders)
        {
            var tasks = order.Status == OrderStatus.Assembly
                ? tasksByOrder[order.Id].Select(MapTask)
                // Assigning the order copies its boxes into a task one to one, so that is what it would be
                : [PlannedTask(order.Id, boxesByOrder[order.Id])];

            result.AddRange(tasks.Select(task => new AssemblyCheckTaskDto
            {
                OrderId = order.Id,
                OrderNumber = order.Number,
                PostingNumber = order.PostingNumber,
                WarehouseId = order.WarehouseId,
                Task = task,
            }));
        }

        var componentDtos = result
            .SelectMany(t => t.Task.Boxes)
            .SelectMany(b => b.Components)
            .ToList();
        var containsUnit = await catalog.ComputeContainsUnitAsync(
            componentDtos.Select(c => c.CatalogItemId).Distinct().ToList(), ct);
        foreach (var component in componentDtos)
            component.ContainsUnit = containsUnit.GetValueOrDefault(component.CatalogItemId);

        return new AssemblyCheckPreviewDto { Tasks = result };
    }

    private static AssemblyTaskDto MapTask(AssemblyTask task) => new()
    {
        Id = task.Id,
        Status = task.Status,
        AssignedToId = task.AssignedToId,
        AssignedToName = task.AssignedTo?.FullName,
        Boxes = task.Boxes
            .Select(box => new AssemblyTaskBoxDto
            {
                Id = box.Id,
                OrderBoxId = box.OrderBoxId,
                OrderBoxLabel = box.OrderBox.Label,
                Components = box.Components
                    .Select(c => ComponentDto(
                        c.Id, c.CatalogItem, c.Quantity - OrderService.CountFulfilledQty(c.Fulfillments)))
                    .ToList(),
            })
            .ToList(),
    };

    /// <summary>The task an order would get on assignment — ids are the order's own, since no task exists yet.</summary>
    private static AssemblyTaskDto PlannedTask(Guid orderId, IEnumerable<OrderBox> boxes) => new()
    {
        Id = orderId,
        Status = AssemblyTaskStatus.Pending,
        Boxes = boxes
            .Select(box => new AssemblyTaskBoxDto
            {
                Id = box.Id,
                OrderBoxId = box.Id,
                OrderBoxLabel = box.Label,
                Components = box.Components
                    .Select(c => ComponentDto(c.Id, c.CatalogItem, c.Quantity))
                    .ToList(),
            })
            .ToList(),
    };

    private static AssemblyTaskBoxComponentDto ComponentDto(Guid id, CatalogItem item, int remaining) => new()
    {
        Id = id,
        CatalogItemId = item.Id,
        CatalogItemName = item.FullName,
        CatalogItemType = item.Type,
        Quantity = Math.Max(0, remaining),
    };

    public async Task<AssemblyCheckResponse> CheckAsync(
        IReadOnlyList<AssemblyCheckItemRequest> items, CancellationToken ct = default)
    {
        var itemIds = items.Select(i => i.CatalogItemId).Distinct().ToList();
        var types = await db.CatalogItems
            .AsNoTracking()
            .Where(i => itemIds.Contains(i.Id))
            .ToDictionaryAsync(i => i.Id, i => i.Type, ct);

        var demand = new Dictionary<(Guid NodeId, Guid CatalogItemId), int>();
        foreach (var item in items)
        {
            if (!types.TryGetValue(item.CatalogItemId, out var type))
                continue;

            foreach (var (nodeId, catalogItemId, quantity) in EnumerateStockDemand(item.CatalogItemId, type, item.Fulfillment))
                demand[(nodeId, catalogItemId)] = demand.GetValueOrDefault((nodeId, catalogItemId)) + quantity;
        }

        var stock = await ReadStockAsync(demand.Keys, ct);

        var errors = new List<(string Name, AppFieldError Error)>();
        foreach (var ((nodeId, catalogItemId), requested) in demand)
        {
            var available = stock.GetValueOrDefault((nodeId, catalogItemId));
            if (available >= requested)
                continue;

            var shortage = await inventory.DescribeShortageAsync(nodeId, catalogItemId, available, requested, ct);
            errors.Add((shortage.CatalogItemName, AppProblems.MakeError(ErrorCode.InsufficientInventory,
                shortage.Message, shortage.ToArgs())));
        }

        return new AssemblyCheckResponse
        {
            InsufficientInventoryErrors = errors
                .OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
                .Select(e => e.Error)
                .ToList(),
        };
    }
}
