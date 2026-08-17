using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Infrastructure;
using ProjectWarehouse.Server.Models;
using ProjectWarehouse.Server.Services;

namespace ProjectWarehouse.Server.Controllers;

[Route("api/commoncontent")]
public class CommonContentController(IMapper mapper, IUserQueryFilterService queryFilter) : AppControllerBase
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

        var warehousesQueryable = await queryFilter.GetWarehousesAsync(User, ct);
        var warehouses = await warehousesQueryable.ProjectTo<AppEntity>(mapper.ConfigurationProvider).Take(2).ToListAsync(ct);
        list.AddRange(warehouses);

        var receiptsQueryable = await queryFilter.GetReceiptsAsync(User, ct);
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
        var warehousesQueryable = await queryFilter.GetWarehousesAsync(User, ct);
        var receiptsQueryable = await queryFilter.GetReceiptsAsync(User, ct);
        var catalogQueryable = await queryFilter.GetCatalogItemsAsync(User, ct);
        var marketplacesAccountsQueryable = await queryFilter.GetMarketplaceAccountsAsync(User, ct);
        var usersQueryable = await queryFilter.GetUsersAsync(User, ct);
        var stocktakesQueryable = await queryFilter.GetStocktakesAsync(User, ct);

        var warehousesResults = await Search(warehousesQueryable, searchString, ct);
        var receiptsResults = await Search(receiptsQueryable, searchString, ct);
        var catalogResults = await Search(catalogQueryable, searchString, ct);
        var marketplacesAccountsResults = await Search(marketplacesAccountsQueryable, searchString, ct);
        var usersResults = await Search(usersQueryable, searchString, ct);
        var stocktakesResults = await Search(stocktakesQueryable, searchString, ct);

        return Ok(warehousesResults.Union(receiptsResults).Union(catalogResults).Union(marketplacesAccountsResults).Union(usersResults).Union(stocktakesResults).Take(10));
    }


    private Task<List<AppEntity>> Search<T>(IQueryable<T> queryable, [FromQuery] string searchString, CancellationToken ct = default)
    {
        return queryable.ProjectTo<AppEntityWithSearchString>(mapper.ConfigurationProvider)
            .WhereMatchesSearch(x => x.SearchString, searchString)
            .Select(x => x.AppEntity)
            .Take(10)
            .ToListAsync(ct);
    }
}
