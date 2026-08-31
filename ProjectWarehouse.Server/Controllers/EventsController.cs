using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProjectWarehouse.Server.Data;
using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Infrastructure;
using ProjectWarehouse.Server.Models;
using ProjectWarehouse.Server.Models.Events;
using ProjectWarehouse.Server.Services;

namespace ProjectWarehouse.Server.Controllers;

[Route("api/events")]
[TimeZoneAware]
public class EventsController(
    IMapper mapper,
    IUserQueryFilterService queryFilter,
    IWarehouseTimeZoneResolver timeZones) : AppControllerBase
{
    /// <summary>Calendar events: planned receipts and stocktakes.</summary>
    /// <remarks>
    /// Days are cut in the zone sent as <c>X-Time-Zone</c>, otherwise in the server's — a stocktake
    /// finished in the evening would otherwise land on the wrong day. The calendar spans every warehouse,
    /// so no warehouse zone can apply here.
    /// Query params: <c>startDate</c>, <c>endDate</c> (both optional, inclusive). Canceled documents and
    /// receipt drafts are never returned.
    /// Rows are narrowed to what the caller may see by <see cref="IUserQueryFilterService"/> rather than by an
    /// up-front permission check, so a user with no receipt or stocktake access gets an empty list, not a 403.
    /// The endpoint produces no error code of its own.
    /// </remarks>
    [HttpGet]
    [Authorize]
    [ProducesResponseType<IReadOnlyList<EventDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetEvents(CancellationToken ct = default, DateOnly? startDate = null,
        DateOnly? endDate = null)
    {
        // The offset travels into the projection through AutoMapper, so it has to be known before the query
        // is built rather than applied to the result.
        var offsetMinutes = (await timeZones.ResolveAsync(null, ct)).CurrentOffsetMinutes();

        var receiptsQueryable = await queryFilter.GetReceiptsAsync(User, ct);
        var receiptsEvents = await receiptsQueryable
            .Where(x => x.PlannedDeliveryDate != null)
            .Where(x => x.Status != ReceiptStatus.Canceled && x.Status != ReceiptStatus.Draft)
            .ProjectTo<EventDto>(mapper.ConfigurationProvider)
            .Where(x => startDate == null || x.StartDate >= startDate)
            .Where(x => endDate == null || x.EndDate <= endDate)
            .ToListAsync(ct);

        var stocktakesQueryable = await queryFilter.GetStocktakesAsync(User, ct);
        var stocktakesEvents = await stocktakesQueryable
            .Where(x => x.Status != StocktakeStatus.Canceled)
            .Where(x => x.FinishedAt != null
                        || (x.Type == StocktakeType.Scheduled
                            && x.Status != StocktakeStatus.Draft
                            && x.PlannedDate != null))
            .ProjectTo<EventDto>(mapper.ConfigurationProvider, new { offsetMinutes })
            .Where(x => startDate == null || x.StartDate >= startDate)
            .Where(x => endDate == null || x.EndDate <= endDate)
            .ToListAsync(ct);
        
        var ordersQueryable = await queryFilter.GetOrdersAsync(User, ct);
        
        var fbsOrdersGroupedEvents = await ordersQueryable
            .Where(x => x.Type == OrderType.FBS)
            .GroupBy(x => DateOnly.FromDateTime(x.EffectiveDate.AddMinutes(offsetMinutes)))
            .Select(x => new EventDto
            {
                AppEntity = new AppEntity
                {
                    Type = AppEntityType.FbsOrdersGrouped,
                    AdditionalFields = new Dictionary<string, object>
                    {
                        { "totalOrders", x.Count(o => o.Status != OrderStatus.Draft) },
                        { "completedOrders", x.Where(x => x.TerminalStatus).Count(o => o.Status != OrderStatus.Draft) },
                    },
                },
                StartDate = x.Key,
                EndDate = x.Key,
            })
            .Where(x => startDate == null || x.StartDate >= startDate)
            .Where(x => endDate == null || x.EndDate <= endDate)
            .ToListAsync(ct);
        
        var ordersEvents = await ordersQueryable
            .Where(x => x.Type != OrderType.FBS)
            .ProjectTo<EventDto>(mapper.ConfigurationProvider, new { offsetMinutes })
            .Where(x => startDate == null || x.StartDate >= startDate)
            .Where(x => endDate == null || x.EndDate <= endDate)
            .ToListAsync(ct);

        return Ok(receiptsEvents.Concat(stocktakesEvents).Concat(fbsOrdersGroupedEvents).Concat(ordersEvents).ToList());
    }
}