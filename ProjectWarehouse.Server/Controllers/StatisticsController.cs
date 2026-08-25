using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectWarehouse.Server.Infrastructure;
using ProjectWarehouse.Server.Models;
using ProjectWarehouse.Server.Models.Statistics;
using ProjectWarehouse.Server.Services;

namespace ProjectWarehouse.Server.Controllers;

[Route("api/statistics")]
[TimeZoneAware]
public class StatisticsController(IStockStatisticsService statistics) : AppControllerBase
{
    /// <summary>Daily in/out/transfer totals over a date range.</summary>
    /// <remarks>
    /// Every day of the range is present, including empty ones. Days are cut in the zone of
    /// <c>warehouseId</c> when it has one, otherwise in the zone sent as <c>X-Time-Zone</c>, otherwise in the
    /// server's; the applied zone comes back as <c>timeZoneId</c>. Defaults to the last 30 days; the range
    /// may not exceed 366 days.
    /// Query params come from <c>StockMovementFilterRequest</c>: <c>from</c>, <c>to</c>, <c>warehouseId</c>,
    /// <c>storagePlaceId</c>, <c>nodeId</c>, <c>userId</c>, <c>catalogItemIds</c>, <c>actions</c>,
    /// <c>directions</c>.
    /// Requires <c>statistics.view</c> or <c>statistics.view_assigned</c> — either one grants access and the
    /// warehouses the rows come from are narrowed afterwards; 403 <c>permissionDenied</c> when neither is held.
    /// Returns 422 <c>outOfRange</c> on <c>from</c> when <c>from</c> is later than <c>to</c> or the range
    /// exceeds 366 days (no <c>args</c>). Every statistics endpoint below shares these params and that code.
    /// </remarks>
    [HttpGet("stock-movements/daily")]
    [Authorize]
    [ProducesResponseType<StockMovementDailySeriesDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDaily(
        [FromQuery] StockMovementFilterRequest filter,
        CancellationToken ct = default)
    {
        if (CheckAccess() is { } forbidden) return forbidden;

        try
        {
            return Ok(await statistics.GetDailySeriesAsync(User, filter, ct));
        }
        catch (Infrastructure.ValidationException ex)
        {
            return UnprocessableEntity(ex);
        }
    }

    /// <summary>Pivot: one row per day, one column per catalog item, in/out in each cell.</summary>
    /// <remarks>
    /// Columns are the <c>columnLimit</c> items that moved the most over the range (pass
    /// <c>catalogItemIds</c> to pin them instead). Cells are sparse — a day with no movement of an item
    /// carries no cell. Row totals cover every item the filter matched, so they stay correct even when
    /// <c>hasMoreColumns</c> is true.
    /// Query params: the shared filter plus <c>columnLimit</c> (default 20, range 1..200).
    /// Same access rule and the same 422 <c>outOfRange</c> range errors as <c>stock-movements/daily</c>.
    /// </remarks>
    [HttpGet("stock-movements/pivot")]
    [Authorize]
    [ProducesResponseType<StockMovementPivotDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPivot(
        [FromQuery] StockMovementFilterRequest filter,
        [FromQuery][Range(1, 200)] int columnLimit = 20,
        CancellationToken ct = default)
    {
        if (CheckAccess() is { } forbidden) return forbidden;

        try
        {
            return Ok(await statistics.GetPivotAsync(User, filter, columnLimit, ct));
        }
        catch (Infrastructure.ValidationException ex)
        {
            return UnprocessableEntity(ex);
        }
    }

    /// <summary>Same totals, grouped by one dimension instead of by day.</summary>
    /// <remarks>
    /// Query params: the shared filter plus <c>groupBy</c> (default <c>Action</c>) and <c>limit</c>
    /// (default 20, range 1..200). The range is still cut in the resolved zone.
    /// Same access rule and the same 422 <c>outOfRange</c> range errors as <c>stock-movements/daily</c>.
    /// </remarks>
    [HttpGet("stock-movements/breakdown")]
    [Authorize]
    [ProducesResponseType<IReadOnlyList<StockMovementBreakdownItemDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBreakdown(
        [FromQuery] StockMovementFilterRequest filter,
        [FromQuery] StockMovementGroupBy groupBy = StockMovementGroupBy.Action,
        [FromQuery][Range(1, 200)] int limit = 20,
        CancellationToken ct = default)
    {
        if (CheckAccess() is { } forbidden) return forbidden;

        try
        {
            return Ok(await statistics.GetBreakdownAsync(User, filter, groupBy, limit, ct));
        }
        catch (Infrastructure.ValidationException ex)
        {
            return UnprocessableEntity(ex);
        }
    }

    /// <summary>Raw movement rows behind the numbers, newest first.</summary>
    /// <remarks>
    /// Query params: the shared filter plus <c>page</c> (default 1) and <c>pageSize</c> (default 20, max 200).
    /// Same access rule and the same 422 <c>outOfRange</c> range errors as <c>stock-movements/daily</c>.
    /// </remarks>
    [HttpGet("stock-movements")]
    [Authorize]
    [ProducesResponseType<Paginated<StockMovementDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMovements(
        [FromQuery] StockMovementFilterRequest filter,
        [FromQuery][Range(1, int.MaxValue)] int page = 1,
        [FromQuery][Range(1, 200)] int pageSize = 20,
        CancellationToken ct = default)
    {
        if (CheckAccess() is { } forbidden) return forbidden;

        try
        {
            return Ok(await statistics.GetMovementsAsync(User, filter, page, pageSize, ct));
        }
        catch (Infrastructure.ValidationException ex)
        {
            return UnprocessableEntity(ex);
        }
    }

    /// <summary>
    /// Either permission is enough to reach the endpoints; which warehouses the rows come from is then
    /// decided by <see cref="IUserQueryFilterService"/>.
    /// </summary>
    private ObjectResult? CheckAccess() =>
        User.HasClaim("permission", Permissions.Statistics.View) ||
        User.HasClaim("permission", Permissions.Statistics.ViewAssigned)
            ? null
            : Forbidden();
}
