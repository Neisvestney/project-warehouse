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
public class CommonContentController(
    IMapper mapper, IUserQueryFilterService queryFilter, IGlobalSearchService globalSearch) : AppControllerBase
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
    /// may view, and returns at most 10 results ranked by relevance: substring matches of every token first, then
    /// <c>pg_trgm</c> fuzzy matches, each by word similarity. Every type with a match gets at least one slot.
    /// Fuzzy matching needs at least 3 characters and covers only the 1000 most recent orders.
    /// Requires authentication only — no permission opens or closes the endpoint itself.
    /// No error codes; a missing <c>searchString</c> is a model-binding 422 (<c>required</c>).
    /// </remarks>
    [HttpGet("search")]
    [Authorize]
    [ProducesResponseType<IReadOnlyList<AppEntity>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GlobalSearch([FromQuery] string searchString, CancellationToken ct = default)
    {
        return Ok(await globalSearch.SearchAsync(User, searchString, ct));
    }
}
