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
    /// <remarks>
    /// Requires authentication only; content is narrowed per entity type by what the caller may view, so a
    /// user without warehouse or receipt access simply gets fewer rows rather than a 403.
    /// Returns up to 2 warehouses plus every visible receipt that is either <c>Processing</c> or a
    /// <c>Draft</c> the caller created.
    /// Returns 403 <c>permissionDenied</c> when the token carries no usable <c>sub</c> claim. No other error codes.
    /// </remarks>
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
    /// <remarks>
    /// Query params: <c>searchString</c> (required). Searches warehouses, receipts, catalog items,
    /// marketplace accounts, users and stocktakes, each already filtered to what the caller may view, then
    /// returns at most 10 results overall (up to 10 per source before the union).
    /// Requires authentication only — no permission opens or closes the endpoint itself.
    /// No error codes; a missing <c>searchString</c> is a model-binding 422 (<c>required</c>).
    /// </remarks>
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
