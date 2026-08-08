using System.Security.Claims;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.EntityFrameworkCore;
using ProjectWarehouse.Server.Data;
using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Models.Catalog;
using ProjectWarehouse.Server.Infrastructure;
using ProjectWarehouse.Server.Models;
using ProjectWarehouse.Server.Models.Statistics;

namespace ProjectWarehouse.Server.Services;

public class StockStatisticsService(
    ApplicationDbContext db,
    IMapper mapper,
    IUserQueryFilterService userFilter) : IStockStatisticsService
{
    private const int DefaultDays = 30;
    private const int MaxDays = 366;

    private sealed class DayRow : StockMovementTotalsDto
    {
        public DateTime Date { get; init; }
    }

    private sealed class ItemRow : StockMovementTotalsDto
    {
        public Guid CatalogItemId { get; init; }
    }

    private sealed class DayItemRow : StockMovementTotalsDto
    {
        public DateTime Date { get; init; }
        public Guid CatalogItemId { get; init; }
    }

    public async Task<StockMovementDailySeriesDto> GetDailySeriesAsync(
        ClaimsPrincipal user,
        StockMovementFilterRequest filter,
        CancellationToken ct = default)
    {
        var (query, from, to) = await BuildAsync(user, filter, ct);
        var byDate = await GroupByDayAsync(query, filter.UtcOffsetMinutes, ct);

        var items = EachDay(from, to)
            .Select(day => new StockMovementDailyPointDto
            {
                Date = day,
                InQuantity = byDate.GetValueOrDefault(day)?.InQuantity ?? 0,
                OutQuantity = byDate.GetValueOrDefault(day)?.OutQuantity ?? 0,
                TransferInQuantity = byDate.GetValueOrDefault(day)?.TransferInQuantity ?? 0,
                TransferOutQuantity = byDate.GetValueOrDefault(day)?.TransferOutQuantity ?? 0,
                MovementsCount = byDate.GetValueOrDefault(day)?.MovementsCount ?? 0,
            })
            .ToList();

        return new StockMovementDailySeriesDto
        {
            From = from,
            To = to,
            Items = items,
            Totals = Sum(items),
        };
    }

    public async Task<StockMovementPivotDto> GetPivotAsync(
        ClaimsPrincipal user,
        StockMovementFilterRequest filter,
        int columnLimit,
        CancellationToken ct = default)
    {
        var (query, from, to) = await BuildAsync(user, filter, ct);
        var offsetMinutes = filter.UtcOffsetMinutes;

        // One extra row only to learn whether anything was cut off
        var columnRows = await query
            .GroupBy(m => m.CatalogItemId)
            .Select(g => new ItemRow
            {
                CatalogItemId = g.Key,
                InQuantity = g.Sum(m => m.Direction == StockMovementDirection.In ? m.Quantity : 0),
                OutQuantity = g.Sum(m => m.Direction == StockMovementDirection.Out ? m.Quantity : 0),
                TransferInQuantity = g.Sum(m => m.Direction == StockMovementDirection.TransferIn ? m.Quantity : 0),
                TransferOutQuantity = g.Sum(m => m.Direction == StockMovementDirection.TransferOut ? m.Quantity : 0),
                MovementsCount = g.Count(),
            })
            .OrderByDescending(x => x.InQuantity + x.OutQuantity + x.TransferInQuantity + x.TransferOutQuantity)
            .ThenBy(x => x.CatalogItemId)
            .Take(columnLimit + 1)
            .ToListAsync(ct);

        var hasMoreColumns = columnRows.Count > columnLimit;
        if (hasMoreColumns) columnRows.RemoveAt(columnRows.Count - 1);

        var columnIds = columnRows.Select(c => c.CatalogItemId).ToList();

        var catalogItems = await db.CatalogItems
            .Where(ci => columnIds.Contains(ci.Id))
            .ProjectTo<CatalogItemSummaryDto>(mapper.ConfigurationProvider)
            .ToDictionaryAsync(ci => ci.Id, ct);

        var cells = await query
            .Where(m => columnIds.Contains(m.CatalogItemId))
            .GroupBy(m => new { Date = m.CreatedAt.AddMinutes(offsetMinutes).Date, m.CatalogItemId })
            .Select(g => new DayItemRow
            {
                Date = g.Key.Date,
                CatalogItemId = g.Key.CatalogItemId,
                InQuantity = g.Sum(m => m.Direction == StockMovementDirection.In ? m.Quantity : 0),
                OutQuantity = g.Sum(m => m.Direction == StockMovementDirection.Out ? m.Quantity : 0),
                TransferInQuantity = g.Sum(m => m.Direction == StockMovementDirection.TransferIn ? m.Quantity : 0),
                TransferOutQuantity = g.Sum(m => m.Direction == StockMovementDirection.TransferOut ? m.Quantity : 0),
                MovementsCount = g.Count(),
            })
            .ToListAsync(ct);

        var cellsByDate = cells
            .GroupBy(c => DateOnly.FromDateTime(c.Date))
            .ToDictionary(g => g.Key, g => g.ToList());

        // Row totals cover every item the filter matched, not only the items that made it into a column
        var totalsByDate = await GroupByDayAsync(query, offsetMinutes, ct);

        var rows = EachDay(from, to)
            .Select(day => new StockMovementPivotRowDto
            {
                Date = day,
                Cells = (cellsByDate.GetValueOrDefault(day) ?? [])
                    .Select(c => new StockMovementPivotCellDto
                    {
                        CatalogItemId = c.CatalogItemId,
                        InQuantity = c.InQuantity,
                        OutQuantity = c.OutQuantity,
                        TransferInQuantity = c.TransferInQuantity,
                        TransferOutQuantity = c.TransferOutQuantity,
                        MovementsCount = c.MovementsCount,
                    })
                    .ToList(),
                Total = totalsByDate.GetValueOrDefault(day) ?? new StockMovementTotalsDto(),
            })
            .ToList();

        return new StockMovementPivotDto
        {
            From = from,
            To = to,
            Columns = columnRows
                .Where(c => catalogItems.ContainsKey(c.CatalogItemId))
                .Select(c => new StockMovementPivotColumnDto
                {
                    CatalogItemId = c.CatalogItemId,
                    CatalogItem = catalogItems[c.CatalogItemId],
                    InQuantity = c.InQuantity,
                    OutQuantity = c.OutQuantity,
                    TransferInQuantity = c.TransferInQuantity,
                    TransferOutQuantity = c.TransferOutQuantity,
                    MovementsCount = c.MovementsCount,
                })
                .ToList(),
            Rows = rows,
            Totals = Sum(rows.Select(r => r.Total)),
            HasMoreColumns = hasMoreColumns,
        };
    }

    public async Task<IReadOnlyList<StockMovementBreakdownItemDto>> GetBreakdownAsync(
        ClaimsPrincipal user,
        StockMovementFilterRequest filter,
        StockMovementGroupBy groupBy,
        int limit,
        CancellationToken ct = default)
    {
        var (query, _, _) = await BuildAsync(user, filter, ct);

        // Reduced to a uniform key/label shape first, so the aggregation below is written once.
        // All branches produce the same anonymous type.
        var keyed = groupBy switch
        {
            StockMovementGroupBy.CatalogItem => query.Select(m => new
                { Key = (Guid?)m.CatalogItemId, Label = (string?)m.CatalogItem.FullName, m.Direction, m.Quantity }),
            StockMovementGroupBy.Warehouse => query.Select(m => new
                { Key = m.WarehouseId, Label = (string?)m.Warehouse!.Name, m.Direction, m.Quantity }),
            StockMovementGroupBy.StoragePlace => query.Select(m => new
                { Key = m.StoragePlaceId, Label = (string?)m.StoragePlace!.Name, m.Direction, m.Quantity }),
            StockMovementGroupBy.Node => query.Select(m => new
                { Key = m.StoragePlaceNodeId, Label = (string?)m.StoragePlaceNode!.Name, m.Direction, m.Quantity }),
            StockMovementGroupBy.User => query.Select(m => new
                { Key = m.UserId, Label = (string?)m.User!.FullName, m.Direction, m.Quantity }),
            _ => query.Select(m => new
                { Key = (Guid?)null, Label = (string?)m.Action, m.Direction, m.Quantity }),
        };

        return await keyed
            .GroupBy(x => new { x.Key, x.Label })
            .Select(g => new StockMovementBreakdownItemDto
            {
                Key = g.Key.Key,
                Label = g.Key.Label,
                InQuantity = g.Sum(x => x.Direction == StockMovementDirection.In ? x.Quantity : 0),
                OutQuantity = g.Sum(x => x.Direction == StockMovementDirection.Out ? x.Quantity : 0),
                TransferInQuantity = g.Sum(x => x.Direction == StockMovementDirection.TransferIn ? x.Quantity : 0),
                TransferOutQuantity = g.Sum(x => x.Direction == StockMovementDirection.TransferOut ? x.Quantity : 0),
                MovementsCount = g.Count(),
            })
            .OrderByDescending(x => x.InQuantity + x.OutQuantity + x.TransferInQuantity + x.TransferOutQuantity)
            .ThenBy(x => x.Label)
            .Take(limit)
            .ToListAsync(ct);
    }

    public async Task<Paginated<StockMovementDto>> GetMovementsAsync(
        ClaimsPrincipal user,
        StockMovementFilterRequest filter,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var (query, _, _) = await BuildAsync(user, filter, ct);

        return await query
            .OrderByDescending(m => m.CreatedAt)
            .ThenBy(m => m.Id)
            .ProjectTo<StockMovementDto>(mapper.ConfigurationProvider)
            .ToPaginatedAsync(page, pageSize, ct);
    }

    private static IEnumerable<DateOnly> EachDay(DateOnly from, DateOnly to)
    {
        for (var day = from; day <= to; day = day.AddDays(1))
            yield return day;
    }

    private static StockMovementTotalsDto Sum(IEnumerable<StockMovementTotalsDto> parts)
    {
        var list = parts as IReadOnlyCollection<StockMovementTotalsDto> ?? parts.ToList();
        return new StockMovementTotalsDto
        {
            InQuantity = list.Sum(p => p.InQuantity),
            OutQuantity = list.Sum(p => p.OutQuantity),
            TransferInQuantity = list.Sum(p => p.TransferInQuantity),
            TransferOutQuantity = list.Sum(p => p.TransferOutQuantity),
            MovementsCount = list.Sum(p => p.MovementsCount),
        };
    }

    private static async Task<Dictionary<DateOnly, StockMovementTotalsDto>> GroupByDayAsync(
        IQueryable<StockMovement> query,
        int offsetMinutes,
        CancellationToken ct)
    {
        var rows = await query
            .GroupBy(m => m.CreatedAt.AddMinutes(offsetMinutes).Date)
            .Select(g => new DayRow
            {
                Date = g.Key,
                InQuantity = g.Sum(m => m.Direction == StockMovementDirection.In ? m.Quantity : 0),
                OutQuantity = g.Sum(m => m.Direction == StockMovementDirection.Out ? m.Quantity : 0),
                TransferInQuantity = g.Sum(m => m.Direction == StockMovementDirection.TransferIn ? m.Quantity : 0),
                TransferOutQuantity = g.Sum(m => m.Direction == StockMovementDirection.TransferOut ? m.Quantity : 0),
                MovementsCount = g.Count(),
            })
            .ToListAsync(ct);

        return rows.ToDictionary(r => DateOnly.FromDateTime(r.Date), StockMovementTotalsDto (r) => r);
    }

    /// <summary>
    /// Resolves the day range, converts it to a UTC half-open interval and applies every filter.
    /// The range is converted rather than shifted per row so the index on <c>CreatedAt</c> stays usable.
    /// </summary>
    private async Task<(IQueryable<StockMovement> Query, DateOnly From, DateOnly To)> BuildAsync(
        ClaimsPrincipal user,
        StockMovementFilterRequest filter,
        CancellationToken ct)
    {
        var offset = TimeSpan.FromMinutes(filter.UtcOffsetMinutes);
        var to = filter.To ?? DateOnly.FromDateTime(DateTime.UtcNow + offset);
        var from = filter.From ?? to.AddDays(-(DefaultDays - 1));

        if (from > to)
            throw new ValidationException("from", ErrorCode.OutOfRange,
                "The start of the range must not be later than its end.");

        var days = to.DayNumber - from.DayNumber + 1;
        if (days > MaxDays)
            throw new ValidationException("from", ErrorCode.OutOfRange,
                $"The range must not exceed {MaxDays} days.");

        var fromUtc = DateTime.SpecifyKind(from.ToDateTime(TimeOnly.MinValue) - offset, DateTimeKind.Utc);
        var toUtc = DateTime.SpecifyKind(to.AddDays(1).ToDateTime(TimeOnly.MinValue) - offset, DateTimeKind.Utc);

        var query = (await userFilter.GetStockMovementsAsync(user, ct))
            .Where(m => m.CreatedAt >= fromUtc && m.CreatedAt < toUtc)
            .Where(m => filter.WarehouseId == null || m.WarehouseId == filter.WarehouseId)
            .Where(m => filter.StoragePlaceId == null || m.StoragePlaceId == filter.StoragePlaceId)
            .Where(m => filter.NodeId == null || m.StoragePlaceNodeId == filter.NodeId)
            .Where(m => filter.UserId == null || m.UserId == filter.UserId);

        if (filter.CatalogItemIds is { Length: > 0 } catalogItemIds)
            query = query.Where(m => catalogItemIds.Contains(m.CatalogItemId));

        if (filter.Actions is { Length: > 0 } actions)
            query = query.Where(m => actions.Contains(m.Action));

        if (filter.Directions is { Length: > 0 } directions)
            query = query.Where(m => directions.Contains(m.Direction));

        return (query, from, to);
    }
}
