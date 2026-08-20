using System.ComponentModel.DataAnnotations;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProjectWarehouse.Server.Data;
using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Infrastructure;
using ProjectWarehouse.Server.Infrastructure.Access;
using ProjectWarehouse.Server.Models;
using ProjectWarehouse.Server.Models.Catalog;
using ProjectWarehouse.Server.Models.Inventory;

namespace ProjectWarehouse.Server.Controllers;

[Route("api/inventory-items")]
public class InventoryItemsController(
    ApplicationDbContext db,
    IMapper mapper,
    EntityAccessRegistry access,
    AccessScope scope) : AppControllerBase
{
    /// <summary>List all inventory items aggregated by catalog item.</summary>
    /// <remarks>
    /// Returns one row per distinct CatalogItem with a total Count summed across both item kinds
    /// (Standard, Unit). Supports filtering by warehouse, storage place, node,
    /// catalog item types, tags (OR semantics), and archive state.
    /// Query params: <c>page</c> (default 1), <c>pageSize</c> (default 20, max 200), <c>searchString</c>,
    /// <c>warehouseId</c>, <c>storagePlaceId</c>, <c>nodeId</c>, <c>catalogItemTypes</c>, <c>tagIds</c>,
    /// <c>isArchived</c>, <c>sortBy</c> (default <c>Name</c>), <c>sortOrder</c> (default <c>Asc</c>).
    /// Inventory has no permission of its own: it is gated by <c>warehouses.view</c> or
    /// <c>warehouses.view_assigned</c>, and rows are then narrowed to the assigned warehouses.
    /// Errors: 403 <c>permissionDenied</c> when neither permission is held, 401 <c>tokenInvalid</c> when the
    /// token carries no usable user id. Filtering by a warehouse the caller is not assigned to is not an
    /// error — it just yields an empty page.
    /// </remarks>
    [HttpGet]
    [Authorize]
    [ProducesResponseType<Paginated<InventoryItemSummaryDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery][Range(1, int.MaxValue)] int page = 1,
        [FromQuery][Range(1, 200)] int pageSize = 20,
        [FromQuery] string? searchString = null,
        [FromQuery] Guid? warehouseId = null,
        [FromQuery] Guid? storagePlaceId = null,
        [FromQuery] Guid? nodeId = null,
        [FromQuery] IReadOnlyList<CatalogItemType>? catalogItemTypes = null,
        [FromQuery] IReadOnlyList<Guid>? tagIds = null,
        [FromQuery] bool? isArchived = null,
        [FromQuery] InventoryItemSortBy sortBy = InventoryItemSortBy.Name,
        [FromQuery] SortOrder sortOrder = SortOrder.Asc,
        CancellationToken ct = default)
    {
        // Inventory has no permissions of its own — it is scoped by the warehouse the stock sits in
        if (AccessError(await access.For<Warehouse>().PrecheckAsync(User, AccessLevel.View, ct)) is { } error)
            return error;

        var narrowing = await scope.GetWarehouseNarrowingAsync(User, Permissions.Warehouses.View, ct);
        if (AccessError(narrowing.Verdict) is { } tokenError)
            return tokenError;

        var assignedIds = narrowing.Ids;

        var catalogQuery = db.CatalogItems
            .Include(ci => ci.Group)
            .Where(ci => isArchived == null || ci.IsArchived == isArchived)
            .WhereMatchesSearch(ci => ci.SearchString, searchString);

        if (catalogItemTypes != null && catalogItemTypes.Count > 0)
            catalogQuery = catalogQuery.Where(ci => catalogItemTypes.Contains(ci.Type));

        if (tagIds != null && tagIds.Count > 0)
            catalogQuery = catalogQuery.Where(ci => ci.Tags.Any(t => tagIds.Contains(t.Id)));

        var baseQuery = catalogQuery
            .Select(ci => new
            {
                CatalogItem = ci,
                Count =
                    db.StoragePlacesNodesItemsGroups
                        .Where(g => g.CatalogItemId == ci.Id && g.Count > 0)
                        .Where(g => warehouseId == null || g.StoragePlaceNode.RootStoragePlace.WarehouseId == warehouseId)
                        .Where(g => storagePlaceId == null || g.StoragePlaceNode.RootStoragePlaceId == storagePlaceId)
                        .Where(g => nodeId == null || g.StoragePlaceNodeId == nodeId)
                        .Where(g => assignedIds == null || assignedIds.Contains(g.StoragePlaceNode.RootStoragePlace.WarehouseId))
                        .Sum(g => g.Count)
                    + db.InventoryItems.OfType<UnitInventoryItem>()
                        .Where(u => u.CatalogItemId == ci.Id)
                        .Where(u => u.StoragePlaceNodeId != null)
                        .Where(u => warehouseId == null || u.StoragePlaceNode!.RootStoragePlace.WarehouseId == warehouseId)
                        .Where(u => storagePlaceId == null || u.StoragePlaceNode!.RootStoragePlaceId == storagePlaceId)
                        .Where(u => nodeId == null || u.StoragePlaceNodeId == nodeId)
                        .Where(u => assignedIds == null || assignedIds.Contains(u.StoragePlaceNode!.RootStoragePlace.WarehouseId))
                        .Count(),
            })
            .Where(x => x.Count > 0);

        var query = sortBy switch
        {
            InventoryItemSortBy.Article  => baseQuery.Sort(x => x.CatalogItem.Article, sortOrder).ThenBy(x => x.CatalogItem.Id),
            InventoryItemSortBy.Type     => baseQuery.Sort(x => x.CatalogItem.Type, sortOrder).ThenBy(x => x.CatalogItem.Id),
            InventoryItemSortBy.Count    => baseQuery.Sort(x => x.Count, sortOrder).ThenBy(x => x.CatalogItem.Id),
            InventoryItemSortBy.Name     => baseQuery.Sort(x => x.CatalogItem.Name, sortOrder).ThenBy(x => x.CatalogItem.Id),
            _                            => baseQuery.Sort(x => x.CatalogItem.FullName, sortOrder).ThenBy(x => x.CatalogItem.Id),
        };

        var paginated = await query.ToPaginatedAsync(page, pageSize, ct);

        var result = new Paginated<InventoryItemSummaryDto>
        {
            Items = paginated.Items
                .Select(x => new InventoryItemSummaryDto
                {
                    CatalogItemId = x.CatalogItem.Id,
                    CatalogItem = mapper.Map<CatalogItemSummaryDto>(x.CatalogItem),
                    Count = x.Count,
                })
                .ToList(),
            Total = paginated.Total,
            Page = paginated.Page,
            PageSize = paginated.PageSize,
        };

        return Ok(result);
    }

    /// <summary>List all unit inventory items (individual serialized items).</summary>
    /// <remarks>
    /// Query params: <c>page</c> (default 1), <c>pageSize</c> (default 20, max 200), <c>searchString</c>
    /// (matches the inventory number), <c>warehouseId</c>, <c>storagePlaceId</c>, <c>nodeId</c>,
    /// <c>catalogItemId</c>, <c>sortBy</c> (default <c>InventoryNumber</c>), <c>sortOrder</c> (default <c>Asc</c>).
    /// Detached units — those not sitting in a node — are excluded.
    /// Same access rule and the same 403 <c>permissionDenied</c> / 401 <c>tokenInvalid</c> as the aggregate list.
    /// </remarks>
    [HttpGet("units")]
    [Authorize]
    [ProducesResponseType<Paginated<UnitInventoryItemDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllUnits(
        [FromQuery][Range(1, int.MaxValue)] int page = 1,
        [FromQuery][Range(1, 200)] int pageSize = 20,
        [FromQuery] string? searchString = null,
        [FromQuery] Guid? warehouseId = null,
        [FromQuery] Guid? storagePlaceId = null,
        [FromQuery] Guid? nodeId = null,
        [FromQuery] Guid? catalogItemId = null,
        [FromQuery] UnitInventoryItemSortBy sortBy = UnitInventoryItemSortBy.InventoryNumber,
        [FromQuery] SortOrder sortOrder = SortOrder.Asc,
        CancellationToken ct = default)
    {
        // Inventory has no permissions of its own — it is scoped by the warehouse the stock sits in
        if (AccessError(await access.For<Warehouse>().PrecheckAsync(User, AccessLevel.View, ct)) is { } error)
            return error;

        var narrowing = await scope.GetWarehouseNarrowingAsync(User, Permissions.Warehouses.View, ct);
        if (AccessError(narrowing.Verdict) is { } tokenError)
            return tokenError;

        var assignedIds = narrowing.Ids;

        var unitBaseQuery = db.InventoryItems.OfType<UnitInventoryItem>()
            .Where(u => u.StoragePlaceNodeId != null)
            .Where(u => warehouseId == null || u.StoragePlaceNode!.RootStoragePlace.WarehouseId == warehouseId)
            .Where(u => storagePlaceId == null || u.StoragePlaceNode!.RootStoragePlaceId == storagePlaceId)
            .Where(u => nodeId == null || u.StoragePlaceNodeId == nodeId)
            .Where(u => catalogItemId == null || u.CatalogItemId == catalogItemId)
            .Where(u => assignedIds == null || assignedIds.Contains(u.StoragePlaceNode!.RootStoragePlace.WarehouseId))
            .WhereMatchesSearch(u => u.InventoryNumber, searchString);

        var query = sortBy switch
        {
            UnitInventoryItemSortBy.WarehouseName    => unitBaseQuery.Sort(u => u.StoragePlaceNode!.RootStoragePlace.Warehouse.Name, sortOrder).ThenBy(u => u.Id),
            UnitInventoryItemSortBy.StoragePlaceName => unitBaseQuery.Sort(u => u.StoragePlaceNode!.RootStoragePlace.Name, sortOrder).ThenBy(u => u.Id),
            UnitInventoryItemSortBy.NodeName         => unitBaseQuery.Sort(u => u.StoragePlaceNode!.Name, sortOrder).ThenBy(u => u.Id),
            _                                        => unitBaseQuery.Sort(u => u.InventoryNumber, sortOrder).ThenBy(u => u.Id),
        };

        var paginated = await query
            .ProjectTo<UnitInventoryItemDto>(mapper.ConfigurationProvider)
            .ToPaginatedAsync(page, pageSize, ct);

        return Ok(paginated);
    }
}
