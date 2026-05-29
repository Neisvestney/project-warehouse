using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProjectWarehouse.Server.Data;
using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Infrastructure;
using ProjectWarehouse.Server.Models;

namespace ProjectWarehouse.Server.Controllers;

[Route("api/commoncontent")]
public class CommonContentController(ApplicationDbContext db, IMapper mapper) : AppControllerBase
{
    /// <summary>Get list of AppEntities for home page.</summary>
    [HttpGet]
    [Authorize]
    [ProducesResponseType<IReadOnlyList<AppEntity>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetHomePageContent(CancellationToken ct = default)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Forbidden();
        
        var list = new List<AppEntity>();

        var warehousesQueryable = await GetUserWarehousesQueryable(ct);
        var warehouses = await warehousesQueryable.ProjectTo<AppEntity>(mapper.ConfigurationProvider).Take(2).ToListAsync(ct);
        list.AddRange(warehouses);

        var receiptsQueryable = await GetUserReceiptsQueryable(ct);
        var receipts = await receiptsQueryable
            .Where(x => x.Status == ReceiptStatus.Processing || (x.CreatedById == userId && x.Status == ReceiptStatus.Draft))
            .ProjectTo<AppEntity>(mapper.ConfigurationProvider)
            .ToListAsync(ct);
        list.AddRange(receipts);

        return Ok(list);
    }
    
    
    /// <summary>Global search for entities.</summary>
    [HttpGet("search")]
    [Authorize]
    [ProducesResponseType<IReadOnlyList<AppEntity>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GlobalSearch([FromQuery] string searchString, CancellationToken ct = default)
    {
        var warehousesQueryable = await GetUserWarehousesQueryable(ct);
        var receiptsQueryable = await GetUserReceiptsQueryable(ct);
        var catalogQueryable = await GetUserCatalogItemsQueryable(ct);
        var usersQueryable = await GetUserUsersQueryable(ct);

        var warehousesResults = await Search(warehousesQueryable, searchString, ct);
        var receiptsResults = await Search(receiptsQueryable, searchString, ct);
        var catalogResults = await Search(catalogQueryable, searchString, ct);
        var usersResults = await Search(usersQueryable, searchString, ct);
        
        return Ok(warehousesResults.Union(receiptsResults).Union(catalogResults).Union(usersResults).Take(10));
    }


    private Task<List<AppEntity>> Search<T>(IQueryable<T> queryable, [FromQuery] string searchString, CancellationToken ct = default)
    {
        return queryable.ProjectTo<AppEntityWithSearchString>(mapper.ConfigurationProvider)
            .WhereMatchesSearch(x => x.SearchString, searchString)
            .Select(x => x.AppEntity)
            .Take(10)
            .ToListAsync(ct);
    }


    private async Task<IQueryable<Warehouse>> GetUserWarehousesQueryable(CancellationToken ct)
    {
        var userCanViewAllWarehouses = User.HasClaim("permission", Permissions.Warehouses.View);
        var userCanViewAssignedWarehouses = User.HasClaim("permission", Permissions.Warehouses.ViewAssigned);

        if (userCanViewAllWarehouses)
        {
            return db.Warehouses.AsQueryable();
        }
        
        if (userCanViewAssignedWarehouses)
        {
            var assignedIds = await GetCurrentUserAssignedWarehouseIdsAsync(db, ct);
            if (assignedIds != null)
            {
                return db.Warehouses.Where(x => assignedIds.Contains(x.Id));
            }
        }

        return db.Warehouses.Take(0);
    }
    
    private async Task<IQueryable<Receipt>> GetUserReceiptsQueryable(CancellationToken ct)
    {
        var canViewAll = User.HasClaim("permission", Permissions.Receipts.View);
        var canViewAssigned = User.HasClaim("permission", Permissions.Receipts.ViewAssigned);
        var canProcess = User.HasClaim("permission", Permissions.Receipts.ProcessAssigned);

        if (canViewAll)
        {
            return db.Receipts;
        }

        if (canViewAssigned || canProcess)
        {
            var assignedWarehousesIds = await GetCurrentUserAssignedWarehouseIdsAsync(db, ct);
            if (assignedWarehousesIds != null)
            {
                return db.Receipts
                    .Where(x => assignedWarehousesIds.Contains(x.WarehouseId))
                    .Where(x => canViewAssigned || (x.Status == ReceiptStatus.Processing && canProcess));
            }
        }

        return db.Receipts.Take(0);
    }
    
    private async Task<IQueryable<ApplicationUser>> GetUserUsersQueryable(CancellationToken ct)
    {
        var canViewAll = User.HasClaim("permission", Permissions.Users.View);

        if (canViewAll)
        {
            return db.Users;
        }

        return db.Users.Take(0);
    }
    
    private async Task<IQueryable<CatalogItem>> GetUserCatalogItemsQueryable(CancellationToken ct)
    {
        var canView = User.HasClaim("permission", Permissions.Catalog.View);

        if (canView)
        {
            return db.CatalogItems;
        }

        return db.CatalogItems.Take(0);
    }
}