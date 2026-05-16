using System.ComponentModel.DataAnnotations;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProjectWarehouse.Server.Data;
using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Infrastructure;
using ProjectWarehouse.Server.Infrastructure.ChangeLog;
using ProjectWarehouse.Server.Models;
using ProjectWarehouse.Server.Models.Catalog;

namespace ProjectWarehouse.Server.Controllers;

[Route("api/catalog")]
public class CatalogController(
    ApplicationDbContext db,
    IMapper mapper,
    IChangeLogService<CatalogItemDto> changeLog) : AppControllerBase
{
    /// <summary>List all catalog items (paginated, optionally filtered by name).</summary>
    [HttpGet]
    [Authorize(Policy = Permissions.Catalog.View)]
    [ProducesResponseType<Paginated<CatalogItemSummaryDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery][Range(1, int.MaxValue)] int page = 1,
        [FromQuery][Range(1, 200)] int pageSize = 20,
        [FromQuery] string? searchString = null,
        CancellationToken ct = default)
    {
        var paginated = await db.CatalogItems
            .WhereMatchesSearch(c => c.SearchString, searchString)
            .OrderBy(c => c.Name)
            .ProjectTo<CatalogItemSummaryDto>(mapper.ConfigurationProvider)
            .ToPaginatedAsync(page, pageSize, ct);

        return Ok(paginated);
    }

    /// <summary>Get a catalog item by ID including its characteristics.</summary>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = Permissions.Catalog.View)]
    [ProducesResponseType<CatalogItemDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct = default)
    {
        var item = await db.CatalogItems
            .Include(c => c.Characteristics)
            .FirstOrDefaultAsync(c => c.Id == id, ct);

        if (item is null)
            return NotFound(ErrorCode.CatalogItemNotFound, "Catalog item not found.");

        return Ok(mapper.Map<CatalogItemDto>(item));
    }

    /// <summary>Create a new catalog item with optional characteristics.</summary>
    [HttpPost]
    [Authorize(Policy = Permissions.Catalog.Edit)]
    [ProducesResponseType<CatalogItemDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreateCatalogItemRequest request, CancellationToken ct = default)
    {
        var item = new CatalogItem
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Article = request.Article,
            Barcode = request.Barcode,
            Characteristics = request.Characteristics.Select(c => new CatalogItemWithCharacteristic
            {
                Id = Guid.NewGuid(),
                Characteristic = c.Characteristic,
                Barcode = c.Barcode
            }).ToList()
        };

        db.CatalogItems.Add(item);
        await db.SaveChangesAsync(ct);

        var dto = mapper.Map<CatalogItemDto>(item);
        await changeLog.CompareAndSaveToChangelog(null, dto);

        return CreatedAtAction(nameof(GetById), new { id = item.Id }, dto);
    }

    /// <summary>Update a catalog item and atomically sync its characteristics.</summary>
    /// <remarks>
    /// Characteristic sync rules:
    /// <list type="bullet">
    ///   <item><c>id: null</c> — create new characteristic</item>
    ///   <item><c>id</c> present — update existing characteristic</item>
    ///   <item>existing characteristic not in the list — delete</item>
    /// </list>
    /// Returns 422 <c>catalogItemCharacteristicNotFound</c> if any provided ID does not belong to this item.
    /// </remarks>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = Permissions.Catalog.Edit)]
    [ProducesResponseType<CatalogItemDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCatalogItemRequest request, CancellationToken ct = default)
    {
        var item = await db.CatalogItems
            .Include(c => c.Characteristics)
            .FirstOrDefaultAsync(c => c.Id == id, ct);

        if (item is null)
            return NotFound(ErrorCode.CatalogItemNotFound, "Catalog item not found.");

        var beforeDto = mapper.Map<CatalogItemDto>(item);

        var incomingWithId = request.Characteristics
            .Where(c => c.Id is not null)
            .ToList();

        var unknownIds = request.Characteristics
            .Select((c, i) => (c, i))
            .Where(x => x.c.Id is not null &&
                        item.Characteristics.All(existing => existing.Id != x.c.Id!.Value))
            .ToList();

        if (unknownIds.Count > 0)
        {
            var errors = unknownIds.Select(x =>
                (Field: $"characteristics[{x.i}].id", Code: ErrorCode.CatalogItemCharacteristicNotFound,
                    Message: $"Characteristic '{x.c.Id}' does not belong to this catalog item.",
                    Args: (IReadOnlyDictionary<string, object>?)null));
            return Problem(AppProblems.UnprocessableEntities(errors));
        }

        item.Name = request.Name;
        item.Article = request.Article;
        item.Barcode = request.Barcode;

        var incomingIds = incomingWithId.Select(c => c.Id!.Value).ToHashSet();

        var toDelete = item.Characteristics
            .Where(c => !incomingIds.Contains(c.Id))
            .ToList();

        db.CatalogItemsWithCharacteristics.RemoveRange(toDelete);

        foreach (var incoming in incomingWithId)
        {
            var existing = item.Characteristics.First(c => c.Id == incoming.Id!.Value);
            existing.Characteristic = incoming.Characteristic;
            existing.Barcode = incoming.Barcode;
        }

        var toCreate = request.Characteristics
            .Where(c => c.Id is null)
            .Select(c => new CatalogItemWithCharacteristic
            {
                Id = Guid.NewGuid(),
                Characteristic = c.Characteristic,
                Barcode = c.Barcode,
                CatalogItemId = item.Id
            })
            .ToList();

        db.CatalogItemsWithCharacteristics.AddRange(toCreate);

        await db.SaveChangesAsync(ct);

        await db.Entry(item).Collection(c => c.Characteristics).LoadAsync(ct);

        var afterDto = mapper.Map<CatalogItemDto>(item);
        await changeLog.CompareAndSaveToChangelog(beforeDto, afterDto);

        return Ok(afterDto);
    }

    /// <summary>Delete a catalog item and all its characteristics.</summary>
    /// <remarks>Returns 409 <c>catalogItemIsInUse</c> if the item is currently stored in any warehouse.</remarks>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Permissions.Catalog.Edit)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
    {
        var item = await db.CatalogItems
            .Include(c => c.Characteristics)
            .FirstOrDefaultAsync(c => c.Id == id, ct);

        if (item is null)
            return NotFound(ErrorCode.CatalogItemNotFound, "Catalog item not found.");

        var isInUse = await db.StoragePlacesNodesItemsGroups
            .AnyAsync(g => g.CatalogItemWithCharacteristic.CatalogItemId == id && g.Count > 0, ct);
        if (isInUse)
            return Conflict(ErrorCode.CatalogItemIsInUse, "Cannot delete a catalog item that is stored in a warehouse.");

        var dto = mapper.Map<CatalogItemDto>(item);

        db.CatalogItems.Remove(item);
        await db.SaveChangesAsync(ct);

        await changeLog.CompareAndSaveToChangelog(dto, null);

        return NoContent();
    }
}
