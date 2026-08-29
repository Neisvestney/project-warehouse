using System.Security.Claims;
using ProjectWarehouse.Server.Models;
using ProjectWarehouse.Server.Models.Statistics;

namespace ProjectWarehouse.Server.Services;

/// <summary>
/// Reads the stock movement journal. Every method scopes the query to what the user may
/// see (all warehouses vs. assigned ones) before applying the filter.
/// </summary>
public interface IStockStatisticsService
{
    /// <summary>Per-day totals over the filtered range, gaps filled with zero days.</summary>
    Task<StockMovementDailySeriesDto> GetDailySeriesAsync(
        ClaimsPrincipal user,
        StockMovementFilterRequest filter,
        CancellationToken ct = default);

    /// <summary>
    /// Days down, catalog items across. Columns are the <paramref name="columnLimit"/> items that moved the
    /// most; row totals still cover everything the filter matched.
    /// </summary>
    Task<StockMovementPivotDto> GetPivotAsync(
        ClaimsPrincipal user,
        StockMovementFilterRequest filter,
        int columnLimit,
        CancellationToken ct = default);

    /// <summary>Top <paramref name="limit"/> groups over the filtered range, ordered by total quantity moved.</summary>
    Task<IReadOnlyList<StockMovementBreakdownItemDto>> GetBreakdownAsync(
        ClaimsPrincipal user,
        StockMovementFilterRequest filter,
        StockMovementGroupBy groupBy,
        int limit,
        CancellationToken ct = default);

    /// <summary>Raw journal rows, newest first — the drill-down behind a bar of the chart.</summary>
    Task<Paginated<StockMovementDto>> GetMovementsAsync(
        ClaimsPrincipal user,
        StockMovementFilterRequest filter,
        int page,
        int pageSize,
        CancellationToken ct = default);
}
