using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProjectWarehouse.Server.Data;
using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Infrastructure;
using ProjectWarehouse.Server.Infrastructure.Access;
using ProjectWarehouse.Server.Infrastructure.ChangeLog;
using ProjectWarehouse.Server.Models;
using ProjectWarehouse.Server.Models.Warehouses;

namespace ProjectWarehouse.Server.Controllers;

[Route("api/storagePlaces")]
public class StoragePlacesController(
    ApplicationDbContext db,
    IMapper mapper,
    EntityAccessRegistry access,
    IChangeLogService<StoragePlaceNodeDetailsDto> changeLog) : AppControllerBase
{
    /// <summary>
    /// Existence and access in one step. Storage places carry no permissions of their own — they are
    /// scoped by the warehouse they belong to, <c>warehouses.*_assigned</c> included.
    /// </summary>
    private async Task<IActionResult?> CheckStoragePlaceAccessAsync(
        Guid storagePlaceId, AccessLevel level, CancellationToken ct)
    {
        var rule = access.For<StoragePlaceNode>();

        if (AccessError(await rule.PrecheckAsync(User, level, ct)) is { } prelude)
            return prelude;

        var warehouseId = await db.StoragePlaces
            .Where(sp => sp.Id == storagePlaceId)
            .Select(sp => (Guid?)sp.WarehouseId)
            .FirstOrDefaultAsync(ct);

        if (warehouseId is null)
            return NotFound(ErrorCode.StoragePlaceNotFound, "Storage place not found.");

        return AccessError(await rule.CheckWarehouseAsync(User, level, warehouseId.Value, ct));
    }

    /// <summary>Get a flat list of all nodes for a storage place.</summary>
    /// <remarks>Returns <c>StoragePlaceNodeDto[]</c> ordered by order then name — id, name, parentNodeId (null = root), order.</remarks>
    [HttpGet("{id:guid}/nodes")]
    [Authorize]
    [ProducesResponseType<StoragePlaceNodeDto[]>(StatusCodes.Status200OK)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetNodes(Guid id, [FromQuery] Guid? catalogItemId = null, CancellationToken ct = default)
    {
        if (await CheckStoragePlaceAccessAsync(id, AccessLevel.View, ct) is { } error)
            return error;

        var nodes = await GetFlatNodesAsync(id, catalogItemId, ct);
        return Ok(nodes);
    }

    /// <summary>Add a node to a storage place.</summary>
    /// <remarks>
    /// Body: <c>CreateStoragePlaceNodeRequest</c> — name (required), parentNodeId (optional, null = root node).
    /// Returns the updated flat list.
    /// Returns 422 <c>storagePlaceNodeNotFound</c> (field: <c>parentNodeId</c>) if the parent does not belong to this storage place.
    /// </remarks>
    [HttpPost("{id:guid}/nodes")]
    [Authorize]
    [ProducesResponseType<StoragePlaceNodeDto[]>(StatusCodes.Status200OK)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> AddNode(
        Guid id,
        [FromBody] CreateStoragePlaceNodeRequest request,
        CancellationToken ct = default)
    {
        if (await CheckStoragePlaceAccessAsync(id, AccessLevel.Edit, ct) is { } error)
            return error;

        if (request.ParentNodeId is not null)
        {
            var parentNode = await db.StoragePlacesNodes
                .Include(n => n.ItemsGroups)
                .FirstOrDefaultAsync(n => n.Id == request.ParentNodeId && n.RootStoragePlaceId == id, ct);
            if (parentNode is null)
                return UnprocessableEntity("parentNodeId", ErrorCode.StoragePlaceNodeNotFound,
                    "Parent node not found in this storage place.");
            if (parentNode.ItemsGroups.Any(g => g.Count > 0))
                return UnprocessableEntity("root", ErrorCode.StoragePlaceNodeParentHasItems,
                    "Cannot add a child node to a node that has items stored in it.");
        }

        var node = new StoragePlaceNode
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            RootStoragePlaceId = id,
            ParentNodeId = request.ParentNodeId,
            Order = request.Order
        };

        db.StoragePlacesNodes.Add(node);
        await db.SaveChangesAsync(ct);

        await db.Entry(node).Collection(n => n.ItemsGroups).LoadAsync(ct);
        await changeLog.CompareAndSaveToChangelog(null, mapper.Map<StoragePlaceNodeDetailsDto>(node));

        return Ok(await GetFlatNodesAsync(id, ct: ct));
    }

    /// <summary>Update a node's name or parent.</summary>
    /// <remarks>
    /// Body: <c>UpdateStoragePlaceNodeRequest</c> — name (required), parentNodeId (nullable, null = root node).
    /// Returns the updated flat list.
    /// Returns 422 <c>storagePlaceNodeCyclicParent</c> (field: <c>parentNodeId</c>) if the new parent is a descendant of this node or the node itself.
    /// </remarks>
    [HttpPut("{id:guid}/nodes/{nodeId:guid}")]
    [Authorize]
    [ProducesResponseType<StoragePlaceNodeDto[]>(StatusCodes.Status200OK)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpdateNode(
        Guid id,
        Guid nodeId,
        [FromBody] UpdateStoragePlaceNodeRequest request,
        CancellationToken ct = default)
    {
        if (await CheckStoragePlaceAccessAsync(id, AccessLevel.Edit, ct) is { } error)
            return error;

        var allNodes = await db.StoragePlacesNodes
            .Where(n => n.RootStoragePlaceId == id)
            .ToListAsync(ct);

        var node = allNodes.FirstOrDefault(n => n.Id == nodeId);
        if (node is null)
            return NotFound(ErrorCode.StoragePlaceNodeNotFound, "Storage place node not found.");

        var nodeWithDetails = await db.StoragePlacesNodes
            .FirstAsync(n => n.Id == nodeId, ct);
        var beforeNodeDto = mapper.Map<StoragePlaceNodeDetailsDto>(nodeWithDetails);

        if (request.ParentNodeId is not null)
        {
            if (request.ParentNodeId == nodeId)
                return UnprocessableEntity("parentNodeId", ErrorCode.StoragePlaceNodeCyclicParent,
                    "A node cannot be its own parent.");

            var parentExists = allNodes.Any(n => n.Id == request.ParentNodeId);
            if (!parentExists)
                return UnprocessableEntity("parentNodeId", ErrorCode.StoragePlaceNodeNotFound,
                    "Parent node not found in this storage place.");

            var subtree = GetSubtreeIds(allNodes, nodeId);
            if (subtree.Contains(request.ParentNodeId.Value))
                return UnprocessableEntity("parentNodeId", ErrorCode.StoragePlaceNodeCyclicParent,
                    "Setting this parent would create a cycle.");
        }

        node.Name = request.Name;
        node.ParentNodeId = request.ParentNodeId;
        node.Order = request.Order;
        await db.SaveChangesAsync(ct);

        await changeLog.CompareAndSaveToChangelog(beforeNodeDto, mapper.Map<StoragePlaceNodeDetailsDto>(node));

        return Ok(await GetFlatNodesAsync(id, ct: ct));
    }

    /// <summary>Delete a node. Fails if the node has children — delete them first.</summary>
    /// <remarks>Returns the updated flat list on success.</remarks>
    [HttpDelete("{id:guid}/nodes/{nodeId:guid}")]
    [Authorize]
    [ProducesResponseType<StoragePlaceNodeDto[]>(StatusCodes.Status200OK)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> DeleteNode(Guid id, Guid nodeId, CancellationToken ct = default)
    {
        if (await CheckStoragePlaceAccessAsync(id, AccessLevel.Edit, ct) is { } error)
            return error;

        var node = await db.StoragePlacesNodes
            .Include(n => n.ItemsGroups)
            .FirstOrDefaultAsync(n => n.Id == nodeId && n.RootStoragePlaceId == id, ct);

        if (node is null)
            return NotFound(ErrorCode.StoragePlaceNodeNotFound, "Storage place node not found.");

        var hasChildren = await db.StoragePlacesNodes.AnyAsync(n => n.ParentNodeId == nodeId, ct);
        if (hasChildren)
            return UnprocessableEntity("root", ErrorCode.StoragePlaceNodeHasChildren,
                "Cannot delete a node that has children.");

        if (node.ItemsGroups.Any(g => g.Count > 0))
            return UnprocessableEntity("root", ErrorCode.StoragePlaceNodeHasItems,
                "Cannot delete a node that has items stored in it.");

        var hasInventoryItems = await db.InventoryItems.AnyAsync(i => i.StoragePlaceNodeId == nodeId, ct);
        if (hasInventoryItems)
            return UnprocessableEntity("root", ErrorCode.StoragePlaceNodeHasItems,
                "Cannot delete a node that has items stored in it.");

        var nodeDto = mapper.Map<StoragePlaceNodeDetailsDto>(node);

        db.StoragePlacesNodes.Remove(node);
        await db.SaveChangesAsync(ct);

        await changeLog.CompareAndSaveToChangelog(nodeDto, null);

        return Ok(await GetFlatNodesAsync(id, ct: ct));
    }

    /// <summary>Get a node by ID.</summary>
    [HttpGet("{id:guid}/nodes/{nodeId:guid}")]
    [Authorize]
    [ProducesResponseType<StoragePlaceNodeDetailsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetNodeDetails(Guid id, Guid nodeId, CancellationToken ct = default)
    {
        if (await CheckStoragePlaceAccessAsync(id, AccessLevel.View, ct) is { } error)
            return error;

        var allNodes = await db.StoragePlacesNodes
            .Where(n => n.RootStoragePlaceId == id)
            .ToListAsync(ct);

        var node = allNodes.FirstOrDefault(n => n.Id == nodeId);
        if (node is null)
            return NotFound(ErrorCode.StoragePlaceNodeNotFound, "Storage place node not found.");

        await db.Entry(node).Reference(n => n.RootStoragePlace).LoadAsync(ct);

        var nodeById = allNodes.ToDictionary(n => n.Id);
        var dto = mapper.Map<StoragePlaceNodeDetailsDto>(node);
        dto.Name = StoragePlaceNodeHelper.BuildPath(node, nodeById);
        return Ok(dto);
    }

    /// <summary>Bulk-update Order for a set of nodes.</summary>
    /// <remarks>
    /// Body: <c>NodeOrderItem[]</c> — nodeId + order pairs.
    /// Only the nodes included in the list are updated; others are unchanged.
    /// Returns 422 <c>storagePlaceNodeNotFound</c> if any nodeId does not belong to this storage place.
    /// </remarks>
    [HttpPut("{id:guid}/nodes/reorder")]
    [Authorize]
    [ProducesResponseType<StoragePlaceNodeDto[]>(StatusCodes.Status200OK)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ReorderNodes(
        Guid id,
        [FromBody] IReadOnlyList<NodeOrderItem> items,
        CancellationToken ct = default)
    {
        if (await CheckStoragePlaceAccessAsync(id, AccessLevel.Edit, ct) is { } error)
            return error;

        var requestedIds = items.Select(x => x.NodeId).ToHashSet();
        var nodes = await db.StoragePlacesNodes
            .Where(n => n.RootStoragePlaceId == id && requestedIds.Contains(n.Id))
            .ToListAsync(ct);

        var foundIds = nodes.Select(n => n.Id).ToHashSet();
        var missing = items
            .Select((x, i) => (x, i))
            .Where(t => !foundIds.Contains(t.x.NodeId))
            .ToList();

        if (missing.Count > 0)
        {
            var errors = missing.Select(t =>
                (Field: $"[{t.i}].nodeId", Code: ErrorCode.StoragePlaceNodeNotFound,
                    Message: $"Node '{t.x.NodeId}' not found in this storage place.",
                    Args: (IReadOnlyDictionary<string, object>?)null));
            return Problem(AppProblems.UnprocessableEntities(errors));
        }

        var orderMap = items.ToDictionary(x => x.NodeId, x => x.Order);
        foreach (var node in nodes)
            node.Order = orderMap[node.Id];

        await db.SaveChangesAsync(ct);
        return Ok(await GetFlatNodesAsync(id, ct: ct));
    }

    private async Task<StoragePlaceNodeDto[]> GetFlatNodesAsync(Guid storagePlaceId, Guid? catalogItemId = null, CancellationToken ct = default)
    {
        var query = db.StoragePlacesNodes
            .Where(n => n.RootStoragePlaceId == storagePlaceId)
            .OrderBy(n => n.Order)
            .ThenBy(n => n.Name)
            .Select(n => new StoragePlaceNodeDto()
            {
                Id = n.Id,
                Name = n.Name,
                ParentNodeId = n.ParentNodeId,
                Order = n.Order,
                TotalItemsCount = catalogItemId == null
                    ? n.TotalItemsCount
                    : n.ItemsGroups.Where(g => g.CatalogItemId == catalogItemId).Sum(g => g.Count) +
                      n.InventoryItems.Count(i => i.CatalogItemId == catalogItemId),
            });
        
        return await query.ToArrayAsync(ct);
    }

    private static HashSet<Guid> GetSubtreeIds(IReadOnlyList<StoragePlaceNode> all, Guid rootId)
    {
        var result = new HashSet<Guid>();
        var queue = new Queue<Guid>();
        queue.Enqueue(rootId);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            result.Add(current);
            foreach (var child in all.Where(n => n.ParentNodeId == current))
                queue.Enqueue(child.Id);
        }
        return result;
    }
}
