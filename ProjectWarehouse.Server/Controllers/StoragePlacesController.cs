using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProjectWarehouse.Server.Data;
using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Infrastructure;
using ProjectWarehouse.Server.Models;
using ProjectWarehouse.Server.Models.Warehouses;

namespace ProjectWarehouse.Server.Controllers;

[Route("api/storagePlaces")]
public class StoragePlacesController(
    ApplicationDbContext db,
    IMapper mapper) : AppControllerBase
{
    /// <summary>Get a flat list of all nodes for a storage place.</summary>
    /// <remarks>Returns <c>StoragePlaceNodeDto[]</c> ordered by name — id, name, parentNodeId (null = root).</remarks>
    [HttpGet("{id:guid}/nodes")]
    [Authorize(Policy = Permissions.Warehouses.View)]
    [ProducesResponseType<StoragePlaceNodeDto[]>(StatusCodes.Status200OK)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetNodes(Guid id, CancellationToken ct = default)
    {
        if (!await db.StoragePlaces.AnyAsync(sp => sp.Id == id, ct))
            return NotFound(ErrorCode.StoragePlaceNotFound, "Storage place not found.");

        var nodes = await GetFlatNodesAsync(id, ct);
        return Ok(nodes);
    }

    /// <summary>Add a node to a storage place.</summary>
    /// <remarks>
    /// Body: <c>CreateStoragePlaceNodeRequest</c> — name (required), parentNodeId (optional, null = root node).
    /// Returns the updated flat list.
    /// Returns 422 <c>storagePlaceNodeNotFound</c> (field: <c>parentNodeId</c>) if the parent does not belong to this storage place.
    /// </remarks>
    [HttpPost("{id:guid}/nodes")]
    [Authorize(Policy = Permissions.Warehouses.Edit)]
    [ProducesResponseType<StoragePlaceNodeDto[]>(StatusCodes.Status200OK)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> AddNode(
        Guid id,
        [FromBody] CreateStoragePlaceNodeRequest request,
        CancellationToken ct = default)
    {
        if (!await db.StoragePlaces.AnyAsync(sp => sp.Id == id, ct))
            return NotFound(ErrorCode.StoragePlaceNotFound, "Storage place not found.");

        if (request.ParentNodeId is not null)
        {
            var parentExists = await db.StoragePlacesNodes
                .AnyAsync(n => n.Id == request.ParentNodeId && n.RootStoragePlaceId == id, ct);
            if (!parentExists)
                return UnprocessableEntity("parentNodeId", ErrorCode.StoragePlaceNodeNotFound,
                    "Parent node not found in this storage place.");
        }

        var node = new StoragePlaceNode
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            RootStoragePlaceId = id,
            ParentNodeId = request.ParentNodeId
        };

        db.StoragePlacesNodes.Add(node);
        await db.SaveChangesAsync(ct);

        return Ok(await GetFlatNodesAsync(id, ct));
    }

    /// <summary>Update a node's name or parent.</summary>
    /// <remarks>
    /// Body: <c>UpdateStoragePlaceNodeRequest</c> — name (required), parentNodeId (nullable, null = root node).
    /// Returns the updated flat list.
    /// Returns 422 <c>storagePlaceNodeCyclicParent</c> (field: <c>parentNodeId</c>) if the new parent is a descendant of this node or the node itself.
    /// </remarks>
    [HttpPut("{id:guid}/nodes/{nodeId:guid}")]
    [Authorize(Policy = Permissions.Warehouses.Edit)]
    [ProducesResponseType<StoragePlaceNodeDto[]>(StatusCodes.Status200OK)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpdateNode(
        Guid id,
        Guid nodeId,
        [FromBody] UpdateStoragePlaceNodeRequest request,
        CancellationToken ct = default)
    {
        if (!await db.StoragePlaces.AnyAsync(sp => sp.Id == id, ct))
            return NotFound(ErrorCode.StoragePlaceNotFound, "Storage place not found.");

        var allNodes = await db.StoragePlacesNodes
            .Where(n => n.RootStoragePlaceId == id)
            .ToListAsync(ct);

        var node = allNodes.FirstOrDefault(n => n.Id == nodeId);
        if (node is null)
            return NotFound(ErrorCode.StoragePlaceNodeNotFound, "Storage place node not found.");

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
        await db.SaveChangesAsync(ct);

        return Ok(await GetFlatNodesAsync(id, ct));
    }

    /// <summary>Delete a node. Fails if the node has children — delete them first.</summary>
    /// <remarks>Returns the updated flat list on success.</remarks>
    [HttpDelete("{id:guid}/nodes/{nodeId:guid}")]
    [Authorize(Policy = Permissions.Warehouses.Edit)]
    [ProducesResponseType<StoragePlaceNodeDto[]>(StatusCodes.Status200OK)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> DeleteNode(Guid id, Guid nodeId, CancellationToken ct = default)
    {
        if (!await db.StoragePlaces.AnyAsync(sp => sp.Id == id, ct))
            return NotFound(ErrorCode.StoragePlaceNotFound, "Storage place not found.");

        var node = await db.StoragePlacesNodes
            .FirstOrDefaultAsync(n => n.Id == nodeId && n.RootStoragePlaceId == id, ct);

        if (node is null)
            return NotFound(ErrorCode.StoragePlaceNodeNotFound, "Storage place node not found.");

        var hasChildren = await db.StoragePlacesNodes.AnyAsync(n => n.ParentNodeId == nodeId, ct);
        if (hasChildren)
            return UnprocessableEntity("nodeId", ErrorCode.StoragePlaceNodeHasChildren,
                "Cannot delete a node that has children.");

        db.StoragePlacesNodes.Remove(node);
        await db.SaveChangesAsync(ct);

        return Ok(await GetFlatNodesAsync(id, ct));
    }

    private async Task<StoragePlaceNodeDto[]> GetFlatNodesAsync(Guid storagePlaceId, CancellationToken ct)
    {
        return await db.StoragePlacesNodes
            .Where(n => n.RootStoragePlaceId == storagePlaceId)
            .OrderBy(n => n.Name)
            .ProjectTo<StoragePlaceNodeDto>(mapper.ConfigurationProvider)
            .ToArrayAsync(ct);
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