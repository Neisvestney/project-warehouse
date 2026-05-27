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
using ProjectWarehouse.Server.Models.Catalog;
using ProjectWarehouse.Server.Models.Inventory;

namespace ProjectWarehouse.Server.Controllers;

[Route("api/inventory-items")]
public class InventoryItemsController(
    ApplicationDbContext db,
    IMapper mapper) : AppControllerBase
{
    /// <summary>List all inventory items aggregated by catalog item.</summary>
    /// <remarks>
    /// Returns one row per distinct CatalogItem with a total Count summed across all three item kinds
    /// (Standard, Unit, AssembledBundle). Supports filtering by warehouse, storage place, node,
    /// catalog item type, and archive state.
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
        [FromQuery] CatalogItemType? catalogItemType = null,
        [FromQuery] bool? isArchived = null,
        [FromQuery] InventoryItemSortBy sortBy = InventoryItemSortBy.Name,
        [FromQuery] SortOrder sortOrder = SortOrder.Asc,
        CancellationToken ct = default)
    {
        var canViewAll = User.HasClaim("permission", Permissions.Warehouses.View);
        var canViewAssigned = User.HasClaim("permission", Permissions.Warehouses.ViewAssigned);

        if (!canViewAll && !canViewAssigned)
            return Forbidden();

        HashSet<Guid>? assignedIds = null;
        if (!canViewAll)
        {
            assignedIds = await GetCurrentUserAssignedWarehouseIdsAsync(db, ct);
            if (assignedIds is null)
                return Unauthorized(ErrorCode.TokenInvalid, "Invalid token.");
        }

        var baseQuery = db.CatalogItems
            .Where(ci => catalogItemType == null || ci.Type == catalogItemType)
            .Where(ci => isArchived == null || ci.IsArchived == isArchived)
            .WhereMatchesSearch(ci => ci.SearchString, searchString)
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
                        .Where(u => warehouseId == null || u.StoragePlaceNode.RootStoragePlace.WarehouseId == warehouseId)
                        .Where(u => storagePlaceId == null || u.StoragePlaceNode.RootStoragePlaceId == storagePlaceId)
                        .Where(u => nodeId == null || u.StoragePlaceNodeId == nodeId)
                        .Where(u => assignedIds == null || assignedIds.Contains(u.StoragePlaceNode.RootStoragePlace.WarehouseId))
                        .Count()
                    + db.InventoryItems.OfType<AssembledBundleInventoryItem>()
                        .Where(ab => ab.CatalogItemId == ci.Id)
                        .Where(ab => warehouseId == null || ab.StoragePlaceNode.RootStoragePlace.WarehouseId == warehouseId)
                        .Where(ab => storagePlaceId == null || ab.StoragePlaceNode.RootStoragePlaceId == storagePlaceId)
                        .Where(ab => nodeId == null || ab.StoragePlaceNodeId == nodeId)
                        .Where(ab => assignedIds == null || assignedIds.Contains(ab.StoragePlaceNode.RootStoragePlace.WarehouseId))
                        .Count(),
            })
            .Where(x => x.Count > 0);

        var query = sortBy switch
        {
            InventoryItemSortBy.Article => baseQuery.Sort(x => x.CatalogItem.Article, sortOrder).ThenBy(x => x.CatalogItem.Id),
            InventoryItemSortBy.Type    => baseQuery.Sort(x => x.CatalogItem.Type, sortOrder).ThenBy(x => x.CatalogItem.Id),
            InventoryItemSortBy.Count   => baseQuery.Sort(x => x.Count, sortOrder).ThenBy(x => x.CatalogItem.Id),
            _                           => baseQuery.Sort(x => x.CatalogItem.Name, sortOrder).ThenBy(x => x.CatalogItem.Id),
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
        [FromQuery] UnitInventoryItemSortBy sortBy = UnitInventoryItemSortBy.Sku,
        [FromQuery] SortOrder sortOrder = SortOrder.Asc,
        CancellationToken ct = default)
    {
        var canViewAll = User.HasClaim("permission", Permissions.Warehouses.View);
        var canViewAssigned = User.HasClaim("permission", Permissions.Warehouses.ViewAssigned);

        if (!canViewAll && !canViewAssigned)
            return Forbidden();

        HashSet<Guid>? assignedIds = null;
        if (!canViewAll)
        {
            assignedIds = await GetCurrentUserAssignedWarehouseIdsAsync(db, ct);
            if (assignedIds is null)
                return Unauthorized(ErrorCode.TokenInvalid, "Invalid token.");
        }

        var unitBaseQuery = db.InventoryItems.OfType<UnitInventoryItem>()
            .Where(u => warehouseId == null || u.StoragePlaceNode.RootStoragePlace.WarehouseId == warehouseId)
            .Where(u => storagePlaceId == null || u.StoragePlaceNode.RootStoragePlaceId == storagePlaceId)
            .Where(u => nodeId == null || u.StoragePlaceNodeId == nodeId)
            .Where(u => catalogItemId == null || u.CatalogItemId == catalogItemId)
            .Where(u => assignedIds == null || assignedIds.Contains(u.StoragePlaceNode.RootStoragePlace.WarehouseId))
            .WhereMatchesSearch(u => u.Sku, searchString);

        var query = sortBy switch
        {
            UnitInventoryItemSortBy.WarehouseName    => unitBaseQuery.Sort(u => u.StoragePlaceNode.RootStoragePlace.Warehouse.Name, sortOrder).ThenBy(u => u.Id),
            UnitInventoryItemSortBy.StoragePlaceName => unitBaseQuery.Sort(u => u.StoragePlaceNode.RootStoragePlace.Name, sortOrder).ThenBy(u => u.Id),
            UnitInventoryItemSortBy.NodeName         => unitBaseQuery.Sort(u => u.StoragePlaceNode.Name, sortOrder).ThenBy(u => u.Id),
            _                                        => unitBaseQuery.Sort(u => u.Sku, sortOrder).ThenBy(u => u.Id),
        };

        var paginated = await query
            .ProjectTo<UnitInventoryItemDto>(mapper.ConfigurationProvider)
            .ToPaginatedAsync(page, pageSize, ct);

        return Ok(paginated);
    }

    /// <summary>List all assembled bundle inventory items (individual bundle instances).</summary>
    [HttpGet("assembled-bundles")]
    [Authorize]
    [ProducesResponseType<Paginated<AssembledBundleInventoryItemDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllAssembledBundles(
        [FromQuery][Range(1, int.MaxValue)] int page = 1,
        [FromQuery][Range(1, 200)] int pageSize = 20,
        [FromQuery] string? searchString = null,
        [FromQuery] Guid? warehouseId = null,
        [FromQuery] Guid? storagePlaceId = null,
        [FromQuery] Guid? nodeId = null,
        [FromQuery] Guid? catalogItemId = null,
        [FromQuery] AssembledBundleInventoryItemSortBy sortBy = AssembledBundleInventoryItemSortBy.WarehouseName,
        [FromQuery] SortOrder sortOrder = SortOrder.Asc,
        CancellationToken ct = default)
    {
        var canViewAll = User.HasClaim("permission", Permissions.Warehouses.View);
        var canViewAssigned = User.HasClaim("permission", Permissions.Warehouses.ViewAssigned);

        if (!canViewAll && !canViewAssigned)
            return Forbidden();

        HashSet<Guid>? assignedIds = null;
        if (!canViewAll)
        {
            assignedIds = await GetCurrentUserAssignedWarehouseIdsAsync(db, ct);
            if (assignedIds is null)
                return Unauthorized(ErrorCode.TokenInvalid, "Invalid token.");
        }

        var bundleBaseQuery = db.InventoryItems.OfType<AssembledBundleInventoryItem>()
            .Where(ab => warehouseId == null || ab.StoragePlaceNode.RootStoragePlace.WarehouseId == warehouseId)
            .Where(ab => storagePlaceId == null || ab.StoragePlaceNode.RootStoragePlaceId == storagePlaceId)
            .Where(ab => nodeId == null || ab.StoragePlaceNodeId == nodeId)
            .Where(ab => catalogItemId == null || ab.CatalogItemId == catalogItemId)
            .Where(ab => assignedIds == null || assignedIds.Contains(ab.StoragePlaceNode.RootStoragePlace.WarehouseId))
            .WhereMatchesSearch(ab => ab.CatalogItem.Name, searchString);

        var query = sortBy switch
        {
            AssembledBundleInventoryItemSortBy.StoragePlaceName => bundleBaseQuery.Sort(ab => ab.StoragePlaceNode.RootStoragePlace.Name, sortOrder).ThenBy(ab => ab.Id),
            AssembledBundleInventoryItemSortBy.NodeName         => bundleBaseQuery.Sort(ab => ab.StoragePlaceNode.Name, sortOrder).ThenBy(ab => ab.Id),
            _                                                   => bundleBaseQuery.Sort(ab => ab.StoragePlaceNode.RootStoragePlace.Warehouse.Name, sortOrder).ThenBy(ab => ab.Id),
        };

        var paginated = await query
            .ProjectTo<AssembledBundleInventoryItemDto>(mapper.ConfigurationProvider)
            .ToPaginatedAsync(page, pageSize, ct);

        return Ok(paginated);
    }
}
