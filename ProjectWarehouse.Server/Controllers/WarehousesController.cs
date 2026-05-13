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
using ProjectWarehouse.Server.Models.Warehouses;

namespace ProjectWarehouse.Server.Controllers;

[Route("api/warehouses")]
public class WarehousesController(
    ApplicationDbContext db,
    IMapper mapper) : AppControllerBase
{
    /// <summary>List all warehouses (paginated, optionally filtered by name).</summary>
    /// <remarks>
    /// Query params: <c>page</c> (default 1), <c>pageSize</c> (default 20, max 200), <c>searchString</c> (optional).
    /// Returns <c>Paginated&lt;WarehouseSummaryDto&gt;</c> — id, name, width, height, storagePlaceCount.
    /// </remarks>
    [HttpGet]
    [Authorize(Policy = Permissions.Warehouses.View)]
    [ProducesResponseType<Paginated<WarehouseSummaryDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery][Range(1, int.MaxValue)] int page = 1,
        [FromQuery][Range(1, 200)] int pageSize = 20,
        [FromQuery] string? searchString = null,
        CancellationToken ct = default)
    {
        var paginated = await db.Warehouses
            .WhereMatchesSearch(w => w.Name, searchString)
            .OrderBy(w => w.Name)
            .ProjectTo<WarehouseSummaryDto>(mapper.ConfigurationProvider)
            .ToPaginatedAsync(page, pageSize, ct);

        return Ok(paginated);
    }

    /// <summary>Get a warehouse by ID including its storage places.</summary>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = Permissions.Warehouses.View)]
    [ProducesResponseType<WarehouseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct = default)
    {
        var warehouse = await db.Warehouses
            .Include(w => w.StoragePlaces)
            .FirstOrDefaultAsync(w => w.Id == id, ct);

        if (warehouse is null)
            return NotFound(ErrorCode.WarehouseNotFound, "Warehouse not found.");

        return Ok(mapper.Map<WarehouseDto>(warehouse));
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
                Height = sp.Height
            }).ToList()
        };

        db.Warehouses.Add(warehouse);
        await db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetById), new { id = warehouse.Id }, mapper.Map<WarehouseDto>(warehouse));
    }

    /// <summary>Update a warehouse and atomically sync its storage places.</summary>
    /// <remarks>
    /// Body: <c>UpdateWarehouseRequest</c>. Storage place sync rules:
    /// <list type="bullet">
    ///   <item><c>id: null</c> — create new storage place</item>
    ///   <item><c>id</c> present — update existing storage place</item>
    ///   <item>existing storage place not in the list — delete</item>
    /// </list>
    /// Returns 422 <c>storagePlaceNotFound</c> if any provided ID does not belong to this warehouse.
    /// </remarks>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = Permissions.Warehouses.Edit)]
    [ProducesResponseType<WarehouseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateWarehouseRequest request, CancellationToken ct = default)
    {
        var warehouse = await db.Warehouses
            .Include(w => w.StoragePlaces)
            .FirstOrDefaultAsync(w => w.Id == id, ct);

        if (warehouse is null)
            return NotFound(ErrorCode.WarehouseNotFound, "Warehouse not found.");

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

        warehouse.Name = request.Name;
        warehouse.Width = request.Width;
        warehouse.Height = request.Height;

        var incomingIds = incomingWithId.Select(sp => sp.Id!.Value).ToHashSet();

        var toDelete = warehouse.StoragePlaces
            .Where(sp => !incomingIds.Contains(sp.Id))
            .ToList();

        db.StoragePlaces.RemoveRange(toDelete);

        foreach (var item in incomingWithId)
        {
            var existing = warehouse.StoragePlaces.First(sp => sp.Id == item.Id!.Value);
            existing.Name = item.Name;
            existing.X = item.X;
            existing.Y = item.Y;
            existing.Width = item.Width;
            existing.Height = item.Height;
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
                WarehouseId = warehouse.Id
            })
            .ToList();

        db.StoragePlaces.AddRange(toCreate);

        await db.SaveChangesAsync(ct);

        await db.Entry(warehouse).Collection(w => w.StoragePlaces).LoadAsync(ct);

        return Ok(mapper.Map<WarehouseDto>(warehouse));
    }

    /// <summary>Delete a warehouse and all its storage places.</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Permissions.Warehouses.Edit)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
    {
        var warehouse = await db.Warehouses.FindAsync([id], ct);

        if (warehouse is null)
            return NotFound(ErrorCode.WarehouseNotFound, "Warehouse not found.");

        db.Warehouses.Remove(warehouse);
        await db.SaveChangesAsync(ct);

        return NoContent();
    }
}