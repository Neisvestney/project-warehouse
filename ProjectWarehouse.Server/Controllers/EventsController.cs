using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProjectWarehouse.Server.Data;
using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Models.Events;
using ProjectWarehouse.Server.Services;

namespace ProjectWarehouse.Server.Controllers;

[Route("api/events")]
public class EventsController(ApplicationDbContext db, IMapper mapper, IUserQueryFilterService queryFilter)
    : AppControllerBase
{
    /// <summary>Calendar events: planned receipts and stocktakes.</summary>
    /// <remarks>
    /// Days are cut in the caller's time zone — pass <c>utcOffsetMinutes</c>, or a stocktake finished in the
    /// evening lands on the wrong day. Same convention as StatisticsController.
    /// </remarks>
    [HttpGet]
    [Authorize]
    [ProducesResponseType<IReadOnlyList<EventDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetEvents(CancellationToken ct = default, DateOnly? startDate = null,
        DateOnly? endDate = null, int utcOffsetMinutes = 0)
    {
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
            .ProjectTo<EventDto>(mapper.ConfigurationProvider, new { offsetMinutes = utcOffsetMinutes })
            .Where(x => startDate == null || x.StartDate >= startDate)
            .Where(x => endDate == null || x.EndDate <= endDate)
            .ToListAsync(ct);

        return Ok(receiptsEvents.Concat(stocktakesEvents).ToList());
    }
}