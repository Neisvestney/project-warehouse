using System.ComponentModel.DataAnnotations;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProjectWarehouse.Server.Data;
using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Infrastructure;
using ProjectWarehouse.Server.Models;
using ProjectWarehouse.Server.Models.InboundOrderProcessing;
using ProjectWarehouse.Server.Models.InboundOrders;
using ProjectWarehouse.Server.Models.Warehouses;

namespace ProjectWarehouse.Server.Controllers;

[Route("api/inbound-order-processing")]
public class InboundOrderProcessingController(
    ApplicationDbContext db,
    IMapper mapper) : AppControllerBase
{
    /// <summary>List inbound orders assigned to the current user.</summary>
    [HttpGet]
    [Authorize(Policy = Permissions.InboundOrders.Process)]
    [ProducesResponseType<Paginated<InboundOrderSummaryDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllAssigned(
        [FromQuery][Range(1, int.MaxValue)] int page = 1,
        [FromQuery][Range(1, 200)] int pageSize = 20,
        [FromQuery] string? searchString = null,
        CancellationToken ct = default)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
            return Unauthorized(ErrorCode.TokenInvalid, "Invalid token.");

        var paginated = await db.InboundOrders
            .Where(o => o.AssignedUsers.Any(u => u.Id == userId.Value))
            .Where(o => o.Status == InboundOrderStatus.Processing)
            .WhereMatchesSearch(o => o.SearchString, searchString)
            .OrderByDescending(o => o.PlannedStartDateTime)
            .ProjectTo<InboundOrderSummaryDto>(mapper.ConfigurationProvider)
            .ToPaginatedAsync(page, pageSize, ct);

        return Ok(paginated);
    }

    /// <summary>Get inbound order processing view with warehouse schema and storage place status.</summary>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = Permissions.InboundOrders.Process)]
    [ProducesResponseType<InboundOrderProcessingDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct = default)
    {
        var order = await db.InboundOrders
            .Include(o => o.AssignedUsers)
            .Include(o => o.Warehouse)
                .ThenInclude(w => w.StoragePlaces)
                    .ThenInclude(sp => sp.StoragePlaceNodes)
                        .ThenInclude(n => n.InboundOrderProcessedItemsGroups.Where(g => g.InboundOrderId == id))
            .FirstOrDefaultAsync(o => o.Id == id, ct);

        if (order is null)
            return NotFound(ErrorCode.InboundOrderNotFound, "Inbound order not found.");

        var userId = GetCurrentUserId();
        if (userId is null || !order.AssignedUsers.Any(u => u.Id == userId.Value))
            return Forbidden(ErrorCode.InboundOrderNotAssigned, "You are not assigned to this order.");

        if (order.Status != InboundOrderStatus.Processing)
            return Conflict(ErrorCode.InboundOrderInvalidStatus, "Order must be in Processing status.");

        var layoutObjects = mapper.Map<List<WarehouseLayoutElementDto>>(order.Warehouse.LayoutObjects);

        var storagePlaces = order.Warehouse.StoragePlaces
            .Select(sp => new ProcessingStoragePlaceDto
            {
                Id = sp.Id,
                Name = sp.Name,
                X = sp.X,
                Y = sp.Y,
                Width = sp.Width,
                Height = sp.Height,
                Rotation = sp.Rotation,
                HasOrderItems = sp.StoragePlaceNodes.Any(n => n.InboundOrderProcessedItemsGroups.Count > 0)
            })
            .ToList();

        var dto = new InboundOrderProcessingDto
        {
            Id = order.Id,
            Number = order.Number,
            Status = order.Status,
            Title = order.Title,
            PlannedStartDateTime = order.PlannedStartDateTime,
            Notes = order.Notes,
            Warehouse = new ProcessingWarehouseDto
            {
                Id = order.Warehouse.Id,
                Name = order.Warehouse.Name,
                Width = order.Warehouse.Width,
                Height = order.Warehouse.Height,
                StoragePlaces = storagePlaces,
                LayoutObjects = layoutObjects
            }
        };

        return Ok(dto);
    }

    /// <summary>Get storage place nodes for a given storage place in this order's warehouse.</summary>
    [HttpGet("{id:guid}/nodes")]
    [Authorize(Policy = Permissions.InboundOrders.Process)]
    [ProducesResponseType<IReadOnlyList<ProcessingStoragePlaceNodeDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> GetStoragePlaceNodes(
        Guid id,
        [FromQuery] Guid storagePlaceId,
        CancellationToken ct = default)
    {
        var order = await db.InboundOrders.FirstOrDefaultAsync(o => o.Id == id, ct);
        if (order is null)
            return NotFound(ErrorCode.InboundOrderNotFound, "Inbound order not found.");

        if (!await IsAssigned(id, ct))
            return Forbidden(ErrorCode.InboundOrderNotAssigned, "You are not assigned to this order.");

        var storagePlaceExists = await db.StoragePlaces
            .AnyAsync(sp => sp.Id == storagePlaceId && sp.WarehouseId == order.WarehouseId, ct);

        if (!storagePlaceExists)
            return NotFound(ErrorCode.StoragePlaceNotFound, "Storage place not found in this order's warehouse.");

        var nodes = await db.StoragePlacesNodes
            .Where(n => n.RootStoragePlaceId == storagePlaceId)
            .OrderBy(n => n.Order)
            .ThenBy(n => n.Name)
            .Select(n => new ProcessingStoragePlaceNodeDto
            {
                Id = n.Id,
                Name = n.Name,
                ParentNodeId = n.ParentNodeId,
                Order = n.Order,
                TotalItemsCount = n.TotalItemsCount,
                HasOrderItems = n.InboundOrderProcessedItemsGroups.Any(g => g.InboundOrderId == id)
            })
            .ToListAsync(ct);

        return Ok(nodes);
    }

    /// <summary>Get storage place node details (including StoragePlaceId and order items) in this order's warehouse.</summary>
    [HttpGet("{id:guid}/nodes/{nodeId:guid}")]
    [Authorize(Policy = Permissions.InboundOrders.Process)]
    [ProducesResponseType<ProcessingStoragePlaceNodeDetailsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStoragePlaceNodeDetails(Guid id, Guid nodeId, CancellationToken ct = default)
    {
        var order = await db.InboundOrders.FirstOrDefaultAsync(o => o.Id == id, ct);
        if (order is null)
            return NotFound(ErrorCode.InboundOrderNotFound, "Inbound order not found.");

        if (!await IsAssigned(id, ct))
            return Forbidden(ErrorCode.InboundOrderNotAssigned, "You are not assigned to this order.");

        var nodeDto = await db.StoragePlacesNodes
            .Where(n => n.Id == nodeId && n.RootStoragePlace.WarehouseId == order.WarehouseId)
            .ProjectTo<StoragePlaceNodeDetailsDto>(mapper.ConfigurationProvider)
            .FirstOrDefaultAsync(ct);

        if (nodeDto is null)
            return NotFound(ErrorCode.StoragePlaceNodeNotFound, "Storage place node not found in this order's warehouse.");

        var orderItems = await db.InboundOrderProcessedItemsGroups
            .Where(g => g.InboundOrderId == id && g.StoragePlaceNodeId == nodeId)
            .ProjectTo<ItemsGroupDto>(mapper.ConfigurationProvider)
            .ToListAsync(ct);

        return Ok(new ProcessingStoragePlaceNodeDetailsDto
        {
            Id = nodeDto.Id,
            Name = nodeDto.Name,
            StoragePlaceId = nodeDto.StoragePlaceId,
            ParentNodeId = nodeDto.ParentNodeId,
            Order = nodeDto.Order,
            ItemsGroups = nodeDto.ItemsGroups,
            OrderItemsGroups = orderItems
        });
    }

    /// <summary>Place items in a storage place node for this order (first placement only).</summary>
    [HttpPost("{id:guid}/nodes/{nodeId:guid}/items")]
    [Authorize(Policy = Permissions.InboundOrders.Process)]
    [ProducesResponseType<IReadOnlyList<ProcessedNodeItemDto>>(StatusCodes.Status201Created)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> PlaceItems(
        Guid id,
        Guid nodeId,
        [FromBody] PlaceItemsRequest request,
        CancellationToken ct = default)
    {
        var (order, nodeExists, guardError) = await LoadOrderAndValidateNode(id, nodeId, ct);
        if (guardError is not null) return guardError;

        if (!nodeExists)
            return NotFound(ErrorCode.StoragePlaceNodeNotFound, "Storage place node not found in this order's warehouse.");

        if (order!.Status != InboundOrderStatus.Processing)
            return Conflict(ErrorCode.InboundOrderInvalidStatus, "Order must be in Processing status.");

        var alreadyHasItems = await db.InboundOrderProcessedItemsGroups
            .AnyAsync(g => g.InboundOrderId == id && g.StoragePlaceNodeId == nodeId, ct);

        if (alreadyHasItems)
            return Conflict(ErrorCode.InboundOrderNodeAlreadyHasItems,
                "Items have already been placed in this node for this order. Use PUT to update.");

        var duplicatePlaceItems = request.Items
            .Select((item, i) => (item, i))
            .GroupBy(x => x.item.CatalogItemWithCharacteristicId)
            .Where(g => g.Count() > 1)
            .SelectMany(g => g.Skip(1))
            .ToList();

        if (duplicatePlaceItems.Count > 0)
        {
            var dupErrors = duplicatePlaceItems.Select(x =>
                (Field: $"items[{x.i}].catalogItemWithCharacteristicId",
                    Code: ErrorCode.CatalogItemCharacteristicDuplicate,
                    Message: $"Duplicate catalog item with characteristic '{x.item.CatalogItemWithCharacteristicId}'.",
                    Args: (IReadOnlyDictionary<string, object>?)null));
            return Problem(AppProblems.UnprocessableEntities(dupErrors));
        }

        var catalogIds = request.Items.Select(i => i.CatalogItemWithCharacteristicId).ToList();
        var validCatalogIds = await db.CatalogItemsWithCharacteristics
            .Where(c => catalogIds.Contains(c.Id))
            .Select(c => c.Id)
            .ToListAsync(ct);

        var invalidItems = request.Items
            .Select((item, i) => (item, i))
            .Where(x => !validCatalogIds.Contains(x.item.CatalogItemWithCharacteristicId))
            .ToList();

        if (invalidItems.Count > 0)
        {
            var errors = invalidItems.Select(x =>
                (Field: $"items[{x.i}].catalogItemWithCharacteristicId",
                    Code: ErrorCode.CatalogItemCharacteristicNotFound,
                    Message: $"Catalog item with characteristic '{x.item.CatalogItemWithCharacteristicId}' not found.",
                    Args: (IReadOnlyDictionary<string, object>?)null));
            return Problem(AppProblems.UnprocessableEntities(errors));
        }

        var existingNodeGroups = await db.StoragePlacesNodesItemsGroups
            .Where(g => g.StoragePlaceNodeId == nodeId && catalogIds.Contains(g.CatalogItemWithCharacteristicId))
            .ToListAsync(ct);
        var nodeGroupsByCharId = existingNodeGroups.ToDictionary(g => g.CatalogItemWithCharacteristicId);

        foreach (var item in request.Items)
        {
            db.InboundOrderProcessedItemsGroups.Add(new InboundOrderProcessedItemsGroup
            {
                Id = Guid.NewGuid(),
                InboundOrderId = id,
                CatalogItemWithCharacteristicId = item.CatalogItemWithCharacteristicId,
                StoragePlaceNodeId = nodeId,
                Count = item.Count
            });

            if (nodeGroupsByCharId.TryGetValue(item.CatalogItemWithCharacteristicId, out var nodeGroup))
            {
                nodeGroup.Count += item.Count;
            }
            else
            {
                db.StoragePlacesNodesItemsGroups.Add(new StoragePlaceNodeItemsGroup
                {
                    Id = Guid.NewGuid(),
                    StoragePlaceNodeId = nodeId,
                    CatalogItemWithCharacteristicId = item.CatalogItemWithCharacteristicId,
                    Count = item.Count
                });
            }
        }

        await db.SaveChangesAsync(ct);

        var result = await BuildProcessedNodeItems(id, nodeId, ct);
        return CreatedAtAction(nameof(GetStoragePlaceNodeDetails), new { id, nodeId }, result);
    }

    /// <summary>Update items placed in a storage place node for this order (delta-based).</summary>
    [HttpPut("{id:guid}/nodes/{nodeId:guid}/items")]
    [Authorize(Policy = Permissions.InboundOrders.Process)]
    [ProducesResponseType<IReadOnlyList<ProcessedNodeItemDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpdateItems(
        Guid id,
        Guid nodeId,
        [FromBody] PlaceItemsRequest request,
        CancellationToken ct = default)
    {
        var (order, nodeExists, guardError) = await LoadOrderAndValidateNode(id, nodeId, ct);
        if (guardError is not null) return guardError;

        if (!nodeExists)
            return NotFound(ErrorCode.StoragePlaceNodeNotFound, "Storage place node not found in this order's warehouse.");

        if (order!.Status != InboundOrderStatus.Processing)
            return Conflict(ErrorCode.InboundOrderInvalidStatus, "Order must be in Processing status.");

        var duplicateUpdateItems = request.Items
            .Select((item, i) => (item, i))
            .GroupBy(x => x.item.CatalogItemWithCharacteristicId)
            .Where(g => g.Count() > 1)
            .SelectMany(g => g.Skip(1))
            .ToList();

        if (duplicateUpdateItems.Count > 0)
        {
            var dupErrors = duplicateUpdateItems.Select(x =>
                (Field: $"items[{x.i}].catalogItemWithCharacteristicId",
                    Code: ErrorCode.CatalogItemCharacteristicDuplicate,
                    Message: $"Duplicate catalog item with characteristic '{x.item.CatalogItemWithCharacteristicId}'.",
                    Args: (IReadOnlyDictionary<string, object>?)null));
            return Problem(AppProblems.UnprocessableEntities(dupErrors));
        }

        var requestIndexByCharId = request.Items
            .Select((item, i) => (item.CatalogItemWithCharacteristicId, i))
            .ToDictionary(x => x.CatalogItemWithCharacteristicId, x => x.i);

        var currentProcessed = await db.InboundOrderProcessedItemsGroups
            .Where(g => g.InboundOrderId == id && g.StoragePlaceNodeId == nodeId)
            .ToListAsync(ct);

        var currentByCharId = currentProcessed.ToDictionary(g => g.CatalogItemWithCharacteristicId);
        var requestByCharId = request.Items.ToDictionary(i => i.CatalogItemWithCharacteristicId);

        var allCharIds = currentByCharId.Keys.Union(requestByCharId.Keys).ToList();

        var nodeGroupsByCharId = await db.StoragePlacesNodesItemsGroups
            .Where(g => g.StoragePlaceNodeId == nodeId && allCharIds.Contains(g.CatalogItemWithCharacteristicId))
            .ToDictionaryAsync(g => g.CatalogItemWithCharacteristicId, ct);

        var insufficientErrors = new List<(string Field, ErrorCode Code, string Message, IReadOnlyDictionary<string, object>? Args)>();

        foreach (var charId in allCharIds)
        {
            var currentCount = currentByCharId.TryGetValue(charId, out var cur) ? cur.Count : 0;
            var newCount = requestByCharId.TryGetValue(charId, out var req) ? req.Count : 0;
            var delta = newCount - currentCount;

            if (delta < 0)
            {
                nodeGroupsByCharId.TryGetValue(charId, out var nodeGroup);
                if (nodeGroup is null || nodeGroup.Count + delta < 0)
                {
                    var fieldIdx = requestIndexByCharId.TryGetValue(charId, out var idx) ? idx : -1;
                    var field = fieldIdx >= 0 ? $"items[{fieldIdx}].count" : "items";
                    insufficientErrors.Add((
                        field,
                        ErrorCode.InboundOrderInsufficientProcessedItems,
                        $"Cannot remove {Math.Abs(delta)} units: only {nodeGroup?.Count ?? 0} are available in this node.",
                        null));
                }
            }
        }

        if (insufficientErrors.Count > 0)
            return Problem(AppProblems.UnprocessableEntities(insufficientErrors));

        foreach (var charId in allCharIds)
        {
            currentByCharId.TryGetValue(charId, out var cur);
            requestByCharId.TryGetValue(charId, out var req);
            var currentCount = cur?.Count ?? 0;
            var newCount = req?.Count ?? 0;
            var delta = newCount - currentCount;

            if (delta == 0) continue;

            nodeGroupsByCharId.TryGetValue(charId, out var nodeGroup);

            if (delta > 0)
            {
                if (cur is null)
                {
                    db.InboundOrderProcessedItemsGroups.Add(new InboundOrderProcessedItemsGroup
                    {
                        Id = Guid.NewGuid(),
                        InboundOrderId = id,
                        CatalogItemWithCharacteristicId = charId,
                        StoragePlaceNodeId = nodeId,
                        Count = newCount
                    });
                }
                else
                {
                    cur.Count = newCount;
                }

                if (nodeGroup is null)
                {
                    db.StoragePlacesNodesItemsGroups.Add(new StoragePlaceNodeItemsGroup
                    {
                        Id = Guid.NewGuid(),
                        StoragePlaceNodeId = nodeId,
                        CatalogItemWithCharacteristicId = charId,
                        Count = delta
                    });
                }
                else
                {
                    nodeGroup.Count += delta;
                }
            }
            else
            {
                if (newCount == 0)
                {
                    if (cur is not null)
                        db.InboundOrderProcessedItemsGroups.Remove(cur);
                }
                else
                {
                    cur!.Count = newCount;
                }

                nodeGroup!.Count += delta; // non-null guaranteed by validation pass
            }
        }

        await db.SaveChangesAsync(ct);

        var result = await BuildProcessedNodeItems(id, nodeId, ct);
        return Ok(result);
    }

    private async Task<bool> IsAssigned(Guid orderId, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        return userId.HasValue && await db.InboundOrders
            .Where(o => o.Id == orderId)
            .AnyAsync(o => o.AssignedUsers.Any(u => u.Id == userId.Value), ct);
    }

    private async Task<(InboundOrder? Order, bool NodeExists, IActionResult? Error)> LoadOrderAndValidateNode(
        Guid orderId, Guid nodeId, CancellationToken ct)
    {
        var order = await db.InboundOrders.FirstOrDefaultAsync(o => o.Id == orderId, ct);
        if (order is null)
            return (null, false, NotFound(ErrorCode.InboundOrderNotFound, "Inbound order not found."));

        if (!await IsAssigned(orderId, ct))
            return (null, false, Forbidden(ErrorCode.InboundOrderNotAssigned, "You are not assigned to this order."));

        var nodeExists = await db.StoragePlacesNodes
            .AnyAsync(n => n.Id == nodeId && n.RootStoragePlace.WarehouseId == order.WarehouseId, ct);

        return (order, nodeExists, null);
    }

    private Task<List<ProcessedNodeItemDto>> BuildProcessedNodeItems(Guid orderId, Guid nodeId, CancellationToken ct)
    {
        return db.InboundOrderProcessedItemsGroups
            .Where(g => g.InboundOrderId == orderId && g.StoragePlaceNodeId == nodeId)
            .ProjectTo<ProcessedNodeItemDto>(mapper.ConfigurationProvider)
            .ToListAsync(ct);
    }
}
