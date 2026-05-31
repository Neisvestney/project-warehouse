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
    [HttpGet]
    [Authorize]
    [ProducesResponseType<IReadOnlyList<EventDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetEvents(CancellationToken ct = default, DateOnly? startDate = null, DateOnly? endDate = null)
    {
        var receiptsQueryable = await queryFilter.GetReceiptsAsync(User, ct);
        var receiptsEvents = await receiptsQueryable
            .Where(x => x.PlannedDeliveryDate != null)
            .Where(x => x.Status != ReceiptStatus.Canceled && x.Status != ReceiptStatus.Draft)
            .ProjectTo<EventDto>(mapper.ConfigurationProvider)
            .Where(x => startDate == null || x.StartDate >= startDate)
            .Where(x => endDate == null || x.EndDate <= endDate)
            .ToListAsync(ct);
        
        return Ok(receiptsEvents);
    }
}