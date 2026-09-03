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
    IUserQueryFilterService userFilter,
    IInventoryService inventoryService,
    IWarehouseTimeZoneResolver timeZones) : IStockStatisticsService
{
    private const int DefaultDays = 30;
    private const int MaxDays = 366;

    /// <summary>The filtered query plus everything the day boundary was decided by.</summary>
    private sealed record MovementScope(
        IQueryable<StockMovement> Query, DateOnly From, DateOnly To, int OffsetMinutes, string TimeZoneId);

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
        var scope = await BuildAsync(user, filter, ct);
        var byDate = await GroupByDayAsync(scope.Query, scope.OffsetMinutes, ct);

        var items = EachDay(scope.From, scope.To)
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
            From = scope.From,
            To = scope.To,
            TimeZoneId = scope.TimeZoneId,
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
        var (query, from, to, offsetMinutes, timeZoneId) = await BuildAsync(user, filter, ct);
        var fromUtc = DateTime.SpecifyKind(
            from.ToDateTime(TimeOnly.MinValue) - TimeSpan.FromMinutes(offsetMinutes), DateTimeKind.Utc);
        var toUtc = DateTime.SpecifyKind(
            to.AddDays(1).ToDateTime(TimeOnly.MinValue) - TimeSpan.FromMinutes(offsetMinutes), DateTimeKind.Utc);

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
        if (filter.CatalogItemIds is not null)
        {
            var unaddedColumnIds = filter.CatalogItemIds.Except(columnIds).ToList();
            while (columnIds.Count < columnLimit && unaddedColumnIds.Count > 0)
            {
                columnIds.Add(unaddedColumnIds[0]);
                unaddedColumnIds.RemoveAt(0);
            }
        }

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

        if (filter.CatalogItemIds is not null)
        {
            foreach (DateOnly day in EachDay(from, to))
            {
                if (!cellsByDate.TryGetValue(day, out var value))
                {
                    value = new List<DayItemRow>();
                    cellsByDate[day] = value;
                }

                foreach (var catalogItemId in filter.CatalogItemIds)
                {
                    if (!value.Any(x => x.CatalogItemId == catalogItemId))
                    {
                        value.Add(new DayItemRow()
                        {
                            CatalogItemId = catalogItemId,
                            Date = toUtc,
                        });
                    }
                }
            }
        }

        // Row totals cover every item the filter matched, not only the items that made it into a column
        var totalsByDate = await GroupByDayAsync(query, offsetMinutes, ct);

        // Balance ignores the display filters (Action/Direction/User/receipt tag) — those only narrow what's
        // *shown*, but every movement, shown or not, moved real stock and has to count toward what's on the
        // shelf. Both the per-item and the total balance walk back over this scope, never over `query`:
        // seeding the walk from unfiltered stock and stepping it with filtered days drifts them apart.
        var stockScope = await BuildStockScopeAsync(user, filter, ct);
        var tailQuery = stockScope.Where(m => m.CreatedAt >= toUtc);

        var stockScopeCells = await stockScope
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

        var stockScopeCellsByDate = stockScopeCells
            .GroupBy(c => DateOnly.FromDateTime(c.Date))
            .ToDictionary(g => g.Key, g => g.ToList());

        // Same scope as the per-item walk, but over every item the filter covers rather than the columns —
        // that is what `currentStockTotal` counts, so the two have to agree.
        var stockScopeNetByDate = await stockScope
            .Where(m => m.CreatedAt >= fromUtc && m.CreatedAt < toUtc)
            .GroupBy(m => m.CreatedAt.AddMinutes(offsetMinutes).Date)
            .Select(g => new
            {
                g.Key,
                Net = g.Sum(m => m.Direction == StockMovementDirection.In || m.Direction == StockMovementDirection.TransferIn
                    ? m.Quantity
                    : -m.Quantity),
            })
            .ToDictionaryAsync(x => DateOnly.FromDateTime(x.Key), x => x.Net, ct);

        var tailNetByItem = await tailQuery
            .Where(m => columnIds.Contains(m.CatalogItemId))
            .GroupBy(m => m.CatalogItemId)
            .Select(g => new
            {
                g.Key,
                Net = g.Sum(m => m.Direction == StockMovementDirection.In || m.Direction == StockMovementDirection.TransferIn
                    ? m.Quantity
                    : -m.Quantity),
            })
            .ToDictionaryAsync(x => x.Key, x => x.Net, ct);

        var tailNetTotal = await tailQuery.SumAsync(
            m => m.Direction == StockMovementDirection.In || m.Direction == StockMovementDirection.TransferIn
                ? m.Quantity
                : -m.Quantity, ct);

        var currentStockByItem = await GetCurrentStockAsync(user, filter, columnIds, ct);
        var currentStockTotal = (await GetCurrentStockAsync(
            user, filter, filter.CatalogItemIds is { Length: > 0 } ids ? ids : null, ct)).Values.Sum();

        var days = EachDay(from, to).ToList();
        var itemBalanceByDay = new Dictionary<DateOnly, Dictionary<Guid, int>>();
        var totalBalanceByDay = new Dictionary<DateOnly, int>();

        var runningItemSuffix = tailNetByItem.ToDictionary(x => x.Key, x => x.Value);
        var runningTotalSuffix = tailNetTotal;
        for (var i = days.Count - 1; i >= 0; i--)
        {
            var day = days[i];

            itemBalanceByDay[day] = columnIds.ToDictionary(
                id => id,
                id => currentStockByItem.GetValueOrDefault(id) - runningItemSuffix.GetValueOrDefault(id));
            totalBalanceByDay[day] = currentStockTotal - runningTotalSuffix;

            foreach (var c in stockScopeCellsByDate.GetValueOrDefault(day) ?? [])
                runningItemSuffix[c.CatalogItemId] = runningItemSuffix.GetValueOrDefault(c.CatalogItemId) + c.Net;
            runningTotalSuffix += stockScopeNetByDate.GetValueOrDefault(day);
        }

        var rows = days
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
                        Balance = itemBalanceByDay[day].GetValueOrDefault(c.CatalogItemId),
                    })
                    .ToList(),
                Total = totalsByDate.GetValueOrDefault(day) ?? new StockMovementTotalsDto(),
                Balance = totalBalanceByDay[day],
            })
            .ToList();

        return new StockMovementPivotDto
        {
            From = from,
            To = to,
            TimeZoneId = timeZoneId,
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
                    Balance = itemBalanceByDay[to].GetValueOrDefault(c.CatalogItemId),
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
        var query = (await BuildAsync(user, filter, ct)).Query;

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
        var query = (await BuildAsync(user, filter, ct)).Query;

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
    /// Same movements as <see cref="BuildAsync"/> but without the Action/Direction/User/date filters —
    /// those only decide what's *shown*, and would otherwise throw off a running stock balance.
    /// </summary>
    private async Task<IQueryable<StockMovement>> BuildStockScopeAsync(
        ClaimsPrincipal user,
        StockMovementFilterRequest filter,
        CancellationToken ct)
    {
        var query = (await userFilter.GetStockMovementsAsync(user, ct))
            .Where(m => filter.WarehouseId == null || m.WarehouseId == filter.WarehouseId)
            .Where(m => filter.StoragePlaceId == null || m.StoragePlaceId == filter.StoragePlaceId)
            .Where(m => filter.NodeId == null || m.StoragePlaceNodeId == filter.NodeId);

        if (filter.CatalogItemIds is { Length: > 0 } catalogItemIds)
            query = query.Where(m => catalogItemIds.Contains(m.CatalogItemId));

        return query;
    }

    /// <summary>Current on-hand quantity per catalog item, scoped to the same location filters and to the
    /// warehouses <paramref name="user"/> may see. <paramref name="restrictToIds"/> narrows further, or
    /// pass null to cover every item the location filter matches.</summary>
    private async Task<Dictionary<Guid, int>> GetCurrentStockAsync(
        ClaimsPrincipal user,
        StockMovementFilterRequest filter,
        IReadOnlyCollection<Guid>? restrictToIds,
        CancellationToken ct)
    {
        var warehouseIds = await (await userFilter.GetWarehousesAsync(user, ct))
            .Select(w => w.Id)
            .ToListAsync(ct);

        return await inventoryService.GetCurrentStockAsync(
            warehouseIds, filter.WarehouseId, filter.StoragePlaceId, filter.NodeId, restrictToIds, ct);
    }

    /// <summary>
    /// Resolves the time zone and the day range, converts the range to a UTC half-open interval and
    /// applies every filter. The range is converted rather than shifted per row so the index on
    /// <c>CreatedAt</c> stays usable.
    /// </summary>
    private async Task<MovementScope> BuildAsync(
        ClaimsPrincipal user,
        StockMovementFilterRequest filter,
        CancellationToken ct)
    {
        var zone = await timeZones.ResolveAsync(filter.WarehouseId, ct);
        var offsetMinutes = zone.CurrentOffsetMinutes();
        var offset = TimeSpan.FromMinutes(offsetMinutes);
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

        if (filter.ReceiptTagIds is { Length: > 0 } receiptTagIds)
            query = query.Where(m => m.Receipt != null && m.Receipt.Tags.Any(t => receiptTagIds.Contains(t.Id)));

        if (filter.Actions is { Length: > 0 } actions)
            query = query.Where(m => actions.Contains(m.Action));

        if (filter.Directions is { Length: > 0 } directions)
            query = query.Where(m => directions.Contains(m.Direction));

        return new MovementScope(query, from, to, offsetMinutes, zone.IanaId());
    }
}
