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
    /// Returns up to 2 warehouses (first by name) plus every visible receipt that is either <c>Processing</c> or a
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
        var warehouses = await warehousesQueryable
            .OrderBy(w => w.Name)
            .ThenBy(w => w.Id)
            .ProjectTo<AppEntity>(mapper.ConfigurationProvider)
            .Take(2)
            .ToListAsync(ct);
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
    /// Query params: <c>searchString</c> (required). Searches orders, warehouses, receipts, catalog items,
    /// marketplace accounts, organizations, users, stocktakes and write-offs, each already filtered to what the caller
    /// may view, and returns at most 10 results grouped by source in that order. Every source with a match gets at
    /// least one slot; the rest are filled in source order.
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
        var organizationsQueryable = await queryFilter.GetOrganizationsAsync(User, ct);
        var ordersQueryable = await queryFilter.GetOrdersAsync(User, ct);
        var writeoffsQueryable = await queryFilter.GetWriteoffsAsync(User, ct);

        // Priority order: earlier sources come first in the response and get leftover slots first.
        List<List<AppEntity>> sources =
        [
            await Search(ordersQueryable, searchString, ct),
            await Search(warehousesQueryable, searchString, ct),
            await Search(receiptsQueryable, searchString, ct),
            await Search(catalogQueryable, searchString, ct),
            await Search(marketplacesAccountsQueryable, searchString, ct),
            await Search(organizationsQueryable, searchString, ct),
            await Search(usersQueryable, searchString, ct),
            await Search(stocktakesQueryable, searchString, ct),
            await Search(writeoffsQueryable, searchString, ct),
        ];

        return Ok(TakeWithPerSourceMinimum(sources, GlobalSearchLimit));
    }

    private const int GlobalSearchLimit = 10;

    /// <summary>
    /// Every non-empty source gets one slot, the remaining slots go to sources in list order;
    /// the result keeps the sources grouped in that order.
    /// </summary>
    private static List<AppEntity> TakeWithPerSourceMinimum(IReadOnlyList<List<AppEntity>> sources, int limit)
    {
        var quotas = new int[sources.Count];
        var left = limit;
        for (var i = 0; i < sources.Count && left > 0; i++)
        {
            if (sources[i].Count == 0) continue;
            quotas[i] = 1;
            left--;
        }
        for (var i = 0; i < sources.Count && left > 0; i++)
        {
            var extra = Math.Min(sources[i].Count - quotas[i], left);
            quotas[i] += extra;
            left -= extra;
        }

        return sources.SelectMany((source, i) => source.Take(quotas[i])).ToList();
    }


    private Task<List<AppEntity>> Search<T>(IQueryable<T> queryable, [FromQuery] string searchString, CancellationToken ct = default)
    {
        return queryable.ProjectTo<AppEntityWithSearchString>(mapper.ConfigurationProvider)
            .WhereMatchesSearch(x => x.SearchString, searchString)
            .Select(x => x.AppEntity)
            .OrderBy(x => x.Name)
            .ThenBy(x => x.Id)
            .Take(10)
            .ToListAsync(ct);
    }
}
