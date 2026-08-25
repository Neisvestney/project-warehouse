using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
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

[Route("api/warehouses")]
public class WarehousesController(
    ApplicationDbContext db,
    IMapper mapper,
    EntityAccessRegistry access,
    IChangeLogService<WarehouseDto> changeLog) : AppControllerBase
{
    private EntityAccessRule<Warehouse> Rule => access.For<Warehouse>();

    /// <summary>List all warehouses (paginated, optionally filtered by name).</summary>
    /// <remarks>
    /// Query params: <c>page</c> (default 1), <c>pageSize</c> (default 20, max 200), <c>searchString</c> (optional).
    /// Returns <c>Paginated&lt;WarehouseSummaryDto&gt;</c> — id, name, width, height, storagePlaceCount.
    /// Requires <c>warehouses.view</c> (all warehouses) or <c>warehouses.view_assigned</c> (assigned warehouses only).
    /// </remarks>
    [HttpGet]
    [Authorize]
    [ProducesResponseType<Paginated<WarehouseSummaryDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery][Range(1, int.MaxValue)] int page = 1,
        [FromQuery][Range(1, 200)] int pageSize = 20,
        [FromQuery] string? searchString = null,
        CancellationToken ct = default)
    {
        if (AccessError(await Rule.PrecheckAsync(User, AccessLevel.View, ct)) is { } error)
            return error;

        var accessible = await Rule.QueryAsync(User, AccessLevel.View, ct);
        var query = accessible.WhereMatchesSearch(w => w.SearchString, searchString);

        var paginated = await query
            .OrderBy(w => w.Name)
            .ProjectTo<WarehouseSummaryDto>(mapper.ConfigurationProvider)
            .ToPaginatedAsync(page, pageSize, ct);

        return Ok(paginated);
    }

    /// <summary>Get a warehouse by ID including its storage places.</summary>
    [HttpGet("{id:guid}")]
    [Authorize]
    [ProducesResponseType<WarehouseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct = default)
    {
        if (AccessError(await Rule.CheckWarehouseAsync(User, AccessLevel.View, id, ct)) is { } error)
            return error;

        var warehouse = await db.Warehouses
            .ProjectTo<WarehouseDto>(mapper.ConfigurationProvider)
            .FirstOrDefaultAsync(w => w.Id == id, ct);

        if (warehouse is null)
            return NotFound(ErrorCode.WarehouseNotFound, "Warehouse not found.");

        return Ok(warehouse);
    }

    /// <summary>Get all storage place nodes for a warehouse mapped to id + full path name for printing.</summary>
    [HttpGet("{id:guid}/print")]
    [Authorize]
    [ProducesResponseType<List<StoragePlaceNodePrintDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByIdForPrint(Guid id, CancellationToken ct = default)
    {
        if (AccessError(await Rule.CheckWarehouseAsync(User, AccessLevel.View, id, ct)) is { } error)
            return error;

        var warehouseExists = await db.Warehouses.AnyAsync(w => w.Id == id, ct);
        if (!warehouseExists)
            return NotFound(ErrorCode.WarehouseNotFound, "Warehouse not found.");

        var nodes = await db.StoragePlacesNodes
            .Where(n => n.RootStoragePlace.WarehouseId == id)
            .Include(n => n.RootStoragePlace)
            .ToListAsync(ct);

        var nodeById = nodes.ToDictionary(n => n.Id);

        var result = nodes
            .Select(n => new StoragePlaceNodePrintDto
            {
                Id   = n.Id,
                Name = StoragePlaceNodeHelper.BuildPath(n, nodeById),
            })
            .OrderBy(n => string.Join(" / ", n.Name))
            .ToList();

        return Ok(result);
    }

    /// <summary>Get the default storage place node for a warehouse.</summary>
    /// <remarks>Returns 404 <c>warehouseNotFound</c> if the warehouse does not exist, or 404 <c>storagePlaceNodeNotFound</c> if no default node is set.</remarks>
    [HttpGet("{id:guid}/default-node")]
    [Authorize]
    [ProducesResponseType<StoragePlaceNodeDetailsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDefaultNode(Guid id, CancellationToken ct = default)
    {
        if (AccessError(await Rule.CheckWarehouseAsync(User, AccessLevel.View, id, ct)) is { } error)
            return error;

        var defaultNodeId = await db.Warehouses
            .Where(w => w.Id == id)
            .Select(w => (Guid?)w.DefaultStoragePlaceNodeId)
            .FirstOrDefaultAsync(ct);

        if (defaultNodeId is null)
            return NotFound(ErrorCode.StoragePlaceNodeNotFound, "No default storage place node set for this warehouse.");

        var allNodes = await db.StoragePlacesNodes
            .Where(n => n.RootStoragePlace.WarehouseId == id)
            .ToListAsync(ct);

        var node = allNodes.FirstOrDefault(n => n.Id == defaultNodeId.Value);
        if (node is null)
            return NotFound(ErrorCode.StoragePlaceNodeNotFound, "Default storage place node not found.");

        await db.Entry(node).Reference(n => n.RootStoragePlace).LoadAsync(ct);
        await db.Entry(node).Collection(n => n.ItemsGroups).LoadAsync(ct);
        await db.Entry(node).Collection(n => n.InventoryItems).LoadAsync(ct);

        var nodeById = allNodes.ToDictionary(n => n.Id);
        var dto = mapper.Map<StoragePlaceNodeDetailsDto>(node);
        dto.Name = StoragePlaceNodeHelper.BuildPath(node, nodeById);
        return Ok(dto);
    }

    /// <summary>Create a new warehouse with optional storage places.</summary>
    /// <remarks>Body: <c>CreateWarehouseRequest</c> — name (required), width, height, storagePlaces (optional).</remarks>
    [HttpPost]
    [Authorize(Policy = Permissions.Warehouses.Edit)]
    [ProducesResponseType<WarehouseDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreateWarehouseRequest request, CancellationToken ct = default)
    {
        var warehouse = new Warehouse
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Width = request.Width,
            Height = request.Height,
            StoragePlaces = request.StoragePlaces.Select(sp => new StoragePlace
            {
                Id = Guid.NewGuid(),
                Name = sp.Name,
                X = sp.X,
                Y = sp.Y,
                Width = sp.Width,
                Height = sp.Height,
                Rotation = sp.Rotation
            }).ToList(),
            LayoutObjects = request.LayoutObjects.Select(lo => new WarehouseLayoutElement
            {
                X = lo.X,
                Y = lo.Y,
                Width = lo.Width,
                Height = lo.Height,
                Rotation = lo.Rotation,
                Type = lo.Type
            }).ToList()
        };

        db.Warehouses.Add(warehouse);
        await db.SaveChangesAsync(ct);

        var dto = mapper.Map<WarehouseDto>(warehouse);
        await changeLog.CompareAndSaveToChangelog(null, dto);

        return CreatedAtAction(nameof(GetById), new { id = warehouse.Id }, dto);
    }

    /// <summary>Update a warehouse and atomically sync its storage places.</summary>
    /// <remarks>
    /// Body: <c>UpdateWarehouseRequest</c>. Storage place sync rules:
    /// <list type="bullet">
    ///   <item><c>id: null</c> — create new storage place</item>
    ///   <item><c>id</c> present — update existing storage place</item>
    ///   <item>existing storage place not in the list — delete</item>
    /// </list>
    /// Returns 422 <c>storagePlaceNotFound</c> if any provided ID does not belong to this warehouse — including
    /// for <c>defaultStoragePlaceNodeId</c>, which must reference a node of this warehouse. Deleting the
    /// referenced node clears the field via cascade.
    /// <c>timeZoneId</c> is an IANA identifier (null falls back to the caller's zone and then to the
    /// server's); an unknown one answers 422 <c>invalidValue</c>. The remaining forecast settings are
    /// written through <c>PUT /api/stock-forecast/settings/{warehouseId}</c>.
    /// Requires <c>warehouses.edit</c> or <c>warehouses.edit_assigned</c> (assigned warehouses only).
    /// </remarks>
    [HttpPut("{id:guid}")]
    [Authorize]
    [ProducesResponseType<WarehouseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateWarehouseRequest request, CancellationToken ct = default)
    {
        if (AccessError(await Rule.CheckWarehouseAsync(User, AccessLevel.Edit, id, ct)) is { } error)
            return error;

        var warehouse = await db.Warehouses
            .Include(w => w.StoragePlaces)
            .Include(w => w.LayoutObjects)
            .FirstOrDefaultAsync(w => w.Id == id, ct);

        if (warehouse is null)
            return NotFound(ErrorCode.WarehouseNotFound, "Warehouse not found.");

        var beforeDto = await db.Warehouses
            .ProjectTo<WarehouseDto>(mapper.ConfigurationProvider)
            .FirstAsync(w => w.Id == id, ct);

        var incomingWithId = request.StoragePlaces
            .Where(sp => sp.Id is not null)
            .ToList();

        var unknownIds = request.StoragePlaces
            .Select((sp, i) => (sp, i))
            .Where(x => x.sp.Id is not null &&
                        warehouse.StoragePlaces.All(existing => existing.Id != x.sp.Id!.Value))
            .ToList();

        if (unknownIds.Count > 0)
        {
            var errors = unknownIds.Select(x =>
                (Field: $"storagePlaces[{x.i}].id", Code: ErrorCode.StoragePlaceNotFound,
                    Message: $"StoragePlace '{x.sp.Id}' does not belong to this warehouse.",
                    Args: (IReadOnlyDictionary<string, object>?)null));
            return Problem(AppProblems.UnprocessableEntities(errors));
        }

        // A stored identifier is a setting somebody typed: a broken one would leave the warehouse
        // silently cutting its days by the server zone forever.
        var timeZoneId = TimeZoneIds.Normalize(request.TimeZoneId);
        if (timeZoneId is not null && !TimeZoneIds.IsKnown(timeZoneId))
            return UnprocessableEntity("timeZoneId", ErrorCode.InvalidValue,
                "Unknown IANA time zone identifier.");

        if (request.DefaultStoragePlaceNodeId.HasValue)
        {
            var nodeExists = await db.StoragePlacesNodes
                .AnyAsync(n => n.Id == request.DefaultStoragePlaceNodeId.Value &&
                               n.RootStoragePlace.WarehouseId == id, ct);
            if (!nodeExists)
                return Problem(AppProblems.UnprocessableEntities([(
                    Field: "defaultStoragePlaceNodeId",
                    Code: ErrorCode.StoragePlaceNotFound,
                    Message: "StoragePlaceNode does not belong to this warehouse.",
                    Args: (IReadOnlyDictionary<string, object>?)null)]));
        }

        warehouse.Name = request.Name;
        warehouse.Width = request.Width;
        warehouse.Height = request.Height;
        warehouse.DefaultStoragePlaceNodeId = request.DefaultStoragePlaceNodeId;
        warehouse.TimeZoneId = timeZoneId;
        warehouse.LayoutObjects.Clear();
        foreach (var lo in request.LayoutObjects)
            warehouse.LayoutObjects.Add(new WarehouseLayoutElement
            {
                X = lo.X,
                Y = lo.Y,
                Width = lo.Width,
                Height = lo.Height,
                Rotation = lo.Rotation,
                Type = lo.Type
            });

        var incomingIds = incomingWithId.Select(sp => sp.Id!.Value).ToHashSet();

        var toDelete = warehouse.StoragePlaces
            .Where(sp => !incomingIds.Contains(sp.Id))
            .ToList();

        if (toDelete.Count > 0)
        {
            var toDeleteIds = toDelete.Select(sp => sp.Id).ToHashSet();
            var hasItems = await db.StoragePlacesNodesItemsGroups
                .AnyAsync(g => toDeleteIds.Contains(g.StoragePlaceNode.RootStoragePlaceId) && g.Count > 0, ct);
            if (hasItems)
                return Conflict(ErrorCode.StoragePlaceHasItems, "Cannot delete storage places that contain items.");
        }

        db.StoragePlaces.RemoveRange(toDelete);

        foreach (var item in incomingWithId)
        {
            var existing = warehouse.StoragePlaces.First(sp => sp.Id == item.Id!.Value);
            existing.Name = item.Name;
            existing.X = item.X;
            existing.Y = item.Y;
            existing.Width = item.Width;
            existing.Height = item.Height;
            existing.Rotation = item.Rotation;
        }

        var toCreate = request.StoragePlaces
            .Where(sp => sp.Id is null)
            .Select(sp => new StoragePlace
            {
                Id = Guid.NewGuid(),
                Name = sp.Name,
                X = sp.X,
                Y = sp.Y,
                Width = sp.Width,
                Height = sp.Height,
                Rotation = sp.Rotation,
                WarehouseId = warehouse.Id
            })
            .ToList();

        db.StoragePlaces.AddRange(toCreate);

        await db.SaveChangesAsync(ct);

        var warehouseDto = await db.Warehouses
            .ProjectTo<WarehouseDto>(mapper.ConfigurationProvider)
            .FirstAsync(w => w.Id == id, ct);

        await changeLog.CompareAndSaveToChangelog(beforeDto, warehouseDto);

        return Ok(warehouseDto);
    }

    /// <summary>Delete a warehouse and all its storage places.</summary>
    /// <remarks>Returns 409 <c>warehouseHasItems</c> if the warehouse contains any stored items.
    /// Requires <c>warehouses.edit</c> or <c>warehouses.edit_assigned</c> (assigned warehouses only).</remarks>
    [HttpDelete("{id:guid}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
    {
        if (AccessError(await Rule.CheckWarehouseAsync(User, AccessLevel.Edit, id, ct)) is { } error)
            return error;

        var warehouseBeforeDto = await db.Warehouses
            .ProjectTo<WarehouseDto>(mapper.ConfigurationProvider)
            .FirstOrDefaultAsync(w => w.Id == id, ct);

        if (warehouseBeforeDto is null)
            return NotFound(ErrorCode.WarehouseNotFound, "Warehouse not found.");

        var hasItems = await db.StoragePlacesNodesItemsGroups
            .AnyAsync(g => g.StoragePlaceNode.RootStoragePlace.WarehouseId == id && g.Count > 0, ct);
        if (hasItems)
            return Conflict(ErrorCode.WarehouseHasItems, "Cannot delete a warehouse that contains items.");

        var warehouse = await db.Warehouses.FindAsync([id], ct);
        db.Warehouses.Remove(warehouse!);
        await db.SaveChangesAsync(ct);

        await changeLog.CompareAndSaveToChangelog(warehouseBeforeDto, null);

        return NoContent();
    }
}