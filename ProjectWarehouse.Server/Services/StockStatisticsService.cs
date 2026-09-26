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

    private const int CellPageSize = 500;
    private static readonly TimeSpan MaxCutLookback = TimeSpan.FromHours(4);
    private const int GroupThreshold = 10;
    private static readonly TimeSpan GroupGap = TimeSpan.FromMinutes(5);

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

    /// <summary>One metric's net for one item on one day. Metrics overlap, so a movement can feed several.</summary>
    private sealed class MetricDayItemRow
    {
        public int MetricIndex { get; init; }
        public DateTime Date { get; init; }
        public Guid CatalogItemId { get; init; }
        public int Net { get; init; }
    }

    /// <summary>A document type whose own cancellation action reverses its stock movements.</summary>
    private enum NettedDocument
    {
        Receipt,
        Order,
    }

    /// <summary>One document's moves of one direction and action, for one item on one day.</summary>
    private sealed class DocumentDayRow
    {
        public DateTime Date { get; init; }
        public Guid CatalogItemId { get; init; }
        public Guid DocumentId { get; init; }
        public StockMovementDirection Direction { get; init; }
        public string Action { get; init; } = null!;
        public int Quantity { get; init; }
        public DateTime FirstAt { get; init; }
        public DateTime LastAt { get; init; }
    }

    /// <summary>
    /// How much of a cell's figures a document move cancelled on its own day takes back, per cell and per
    /// metric. <see cref="MetricDeltaByCell"/> is signed and already accounts for which movements each
    /// metric covers, so it is added to the metric's net as is.
    /// </summary>
    private sealed record SameDayNetting(
        Dictionary<(DateOnly Date, Guid CatalogItemId), int> OffsetByCell,
        Dictionary<Guid, int> OffsetByItem,
        Dictionary<(DateOnly Date, Guid CatalogItemId), int[]> MetricDeltaByCell);

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
        StockMovementPivotRequest request,
        CancellationToken ct = default)
    {
        StockMovementFilterRequest filter = request;
        var metrics = request.Metrics;
        var columnLimit = request.ColumnLimit;

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

        var metricCells = await GetMetricCellsAsync(query, columnIds, metrics, offsetMinutes, ct);

        var netting = await GetSameDayNettingAsync(query, columnIds, metrics, offsetMinutes, ct);
        foreach (var (key, delta) in netting.MetricDeltaByCell)
        {
            if (!metricCells.TryGetValue(key, out var values))
                metricCells[key] = values = new int[metrics.Count];

            for (var i = 0; i < metrics.Count; i++)
                values[i] += delta[i];
        }

        // Balance ignores the display filters (Action/Direction/User/receipt tag) — those only narrow what's
        // *shown*, but every movement, shown or not, moved real stock and has to count toward what's on the
        // shelf. Both the per-item and the total balance walk back over this scope, never over `query`:
        // seeding the walk from unfiltered stock and stepping it with filtered days drifts them apart.
        var stockScope = await BuildStockScopeAsync(user, filter, ct);
        var tailQuery = stockScope.Where(m => m.CreatedAt >= toUtc);

        // Only the days inside the range are ever read back out of this; without the bound it would group
        // an item's entire history and ship every day of it.
        var stockScopeCells = await stockScope
            .Where(m => m.CreatedAt >= fromUtc && m.CreatedAt < toUtc)
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

        var currentStockByItem = await GetCurrentStockAsync(user, filter, columnIds, ct);

        var days = EachDay(from, to).ToList();
        var itemBalanceByDay = new Dictionary<DateOnly, Dictionary<Guid, int>>();

        var runningItemSuffix = tailNetByItem.ToDictionary(x => x.Key, x => x.Value);
        for (var i = days.Count - 1; i >= 0; i--)
        {
            var day = days[i];

            itemBalanceByDay[day] = columnIds.ToDictionary(
                id => id,
                id => currentStockByItem.GetValueOrDefault(id) - runningItemSuffix.GetValueOrDefault(id));

            foreach (var c in stockScopeCellsByDate.GetValueOrDefault(day) ?? [])
                runningItemSuffix[c.CatalogItemId] = runningItemSuffix.GetValueOrDefault(c.CatalogItemId) + c.Net;
        }

        var rows = days
            .Select(day =>
            {
                var cells = (cellsByDate.GetValueOrDefault(day) ?? [])
                    .Select(c => new StockMovementPivotCellDto
                    {
                        CatalogItemId = c.CatalogItemId,
                        InQuantity = c.InQuantity - netting.OffsetByCell.GetValueOrDefault((day, c.CatalogItemId)),
                        OutQuantity = c.OutQuantity - netting.OffsetByCell.GetValueOrDefault((day, c.CatalogItemId)),
                        TransferInQuantity = c.TransferInQuantity,
                        TransferOutQuantity = c.TransferOutQuantity,
                        MovementsCount = c.MovementsCount,
                        Balance = itemBalanceByDay[day].GetValueOrDefault(c.CatalogItemId),
                        Metrics = metricCells.GetValueOrDefault((day, c.CatalogItemId)) ?? Zeros(metrics.Count),
                    })
                    .ToList();

                return new StockMovementPivotRowDto
                {
                    Date = day,
                    Cells = cells,
                    // The total column is the sum of the columns beside it: a figure that does not add up
                    // to what the row shows reads as a bug, whatever else it could honestly count.
                    Total = SumCells(cells, metrics.Count),
                    Balance = itemBalanceByDay[day].Values.Sum(),
                };
            })
            .ToList();

        var metricsByItem = rows
            .SelectMany(r => r.Cells)
            .GroupBy(c => c.CatalogItemId)
            .ToDictionary(g => g.Key, g => SumMetrics(g.Select(c => c.Metrics), metrics.Count));

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
                    InQuantity = c.InQuantity - netting.OffsetByItem.GetValueOrDefault(c.CatalogItemId),
                    OutQuantity = c.OutQuantity - netting.OffsetByItem.GetValueOrDefault(c.CatalogItemId),
                    TransferInQuantity = c.TransferInQuantity,
                    TransferOutQuantity = c.TransferOutQuantity,
                    MovementsCount = c.MovementsCount,
                    Balance = itemBalanceByDay[to].GetValueOrDefault(c.CatalogItemId),
                    Metrics = metricsByItem.GetValueOrDefault(c.CatalogItemId) ?? Zeros(metrics.Count),
                })
                .ToList(),
            Rows = rows,
            Totals = SumTotals(rows.Select(r => r.Total), metrics.Count),
            HasMoreColumns = hasMoreColumns,
        };
    }

    /// <summary>
    /// Every metric in one round trip: each is its own filtered aggregate, and the branches are stitched
    /// with <c>Concat</c> so the provider emits a single UNION ALL instead of a query per metric.
    /// </summary>
    private static async Task<Dictionary<(DateOnly Date, Guid CatalogItemId), int[]>> GetMetricCellsAsync(
        IQueryable<StockMovement> query,
        IReadOnlyCollection<Guid> columnIds,
        IReadOnlyList<StockMovementMetricDto> metrics,
        int offsetMinutes,
        CancellationToken ct)
    {
        var result = new Dictionary<(DateOnly, Guid), int[]>();
        if (metrics.Count == 0) return result;

        var scoped = query.Where(m => columnIds.Contains(m.CatalogItemId));

        IQueryable<MetricDayItemRow>? union = null;
        for (var i = 0; i < metrics.Count; i++)
        {
            var index = i;
            var branch = ApplyMetric(scoped, metrics[i])
                .GroupBy(m => new { Date = m.CreatedAt.AddMinutes(offsetMinutes).Date, m.CatalogItemId })
                .Select(g => new MetricDayItemRow
                {
                    MetricIndex = index,
                    Date = g.Key.Date,
                    CatalogItemId = g.Key.CatalogItemId,
                    Net = g.Sum(m =>
                        m.Direction == StockMovementDirection.In || m.Direction == StockMovementDirection.TransferIn
                            ? m.Quantity
                            : -m.Quantity),
                });

            union = union is null ? branch : union.Concat(branch);
        }

        foreach (var row in await union!.ToListAsync(ct))
        {
            var key = (DateOnly.FromDateTime(row.Date), row.CatalogItemId);
            if (!result.TryGetValue(key, out var values))
                result[key] = values = new int[metrics.Count];
            values[row.MetricIndex] = row.Net;
        }

        return result;
    }

    /// <summary>
    /// A document movement cancelled on the day it was made is reported as never having happened: the
    /// cancelled quantity is taken back out of the document's own figure and the cancellation column shows
    /// zero. Pairing is per document, item and day, capped by what that document actually moved that day —
    /// a cancellation that reaches back to an earlier day stays a movement of its own.
    /// <para>
    /// Pairs are taken from <paramref name="query"/>, so the report nets only what its filters let it see:
    /// a direction, action or user filter that keeps one side of a pair and drops the other leaves the
    /// remaining side raw. Location and document-tag filters never split a pair — both rows share the node
    /// and the document — and neither does the date range, since a netted pair is same-day by definition.
    /// </para>
    /// </summary>
    private async Task<SameDayNetting> GetSameDayNettingAsync(
        IQueryable<StockMovement> query,
        IReadOnlyCollection<Guid> columnIds,
        IReadOnlyList<StockMovementMetricDto> metrics,
        int offsetMinutes,
        CancellationToken ct)
    {
        var offsetByCell = new Dictionary<(DateOnly, Guid), int>();
        var offsetByItem = new Dictionary<Guid, int>();
        var metricDeltaByCell = new Dictionary<(DateOnly, Guid), int[]>();

        // A movement carries at most one document, so the two passes read disjoint rows and their offsets
        // add up: either half of a pair is one In and one Out, whichever side the document's own move is on.
        foreach (var document in Enum.GetValues<NettedDocument>())
        {
            var (baseDirection, cancelDirection, cancelAction) = Shape(document);
            var rows = await FetchDocumentRowsAsync(query, columnIds, document, offsetMinutes, ct);

            var netted = new List<(DateOnly Date, Guid CatalogItemId, Guid DocumentId, int Offset, List<(string Action, int Quantity)> Paired)>();

            foreach (var g in rows.GroupBy(r => (Date: DateOnly.FromDateTime(r.Date), r.CatalogItemId, r.DocumentId)))
            {
                var cancellations = g.Where(r => r.Direction == cancelDirection && r.Action == cancelAction).ToList();
                var cancelled = cancellations.Sum(r => r.Quantity);
                if (cancelled == 0) continue;

                // A cancellation only takes back a move that preceded it, the latest one first — a receipt
                // whose reason changed mid-day moves under two actions, and picking between them by anything
                // other than time gets the attribution backwards half of the time.
                var lastCancelledAt = cancellations.Max(r => r.LastAt);
                var moved = g
                    .Where(r => r.Direction == baseDirection && r.FirstAt < lastCancelledAt)
                    .OrderByDescending(r => r.LastAt)
                    .ThenBy(r => r.Action)
                    .ToList();

                var offset = Math.Min(cancelled, moved.Sum(r => r.Quantity));
                if (offset == 0) continue;

                // The day is aggregated per action, so moves of one action interleaved with several
                // cancellations are one entry and the split between two qualifying actions stays a guess. It
                // moves quantity between metrics only — the In/Out figures take the same offset either way.
                var remaining = offset;
                var paired = new List<(string Action, int Quantity)>();
                foreach (var p in moved)
                {
                    if (remaining == 0) break;
                    var take = Math.Min(remaining, p.Quantity);
                    paired.Add((p.Action, take));
                    remaining -= take;
                }

                var cell = (g.Key.Date, g.Key.CatalogItemId);
                offsetByCell[cell] = offsetByCell.GetValueOrDefault(cell) + offset;
                offsetByItem[g.Key.CatalogItemId] = offsetByItem.GetValueOrDefault(g.Key.CatalogItemId) + offset;
                netted.Add((g.Key.Date, g.Key.CatalogItemId, g.Key.DocumentId, offset, paired));
            }

            if (metrics.Count == 0 || netted.Count == 0) continue;

            var tagsByDocument = await LoadDocumentTagsAsync(
                document, metrics, netted.Select(n => n.DocumentId).Distinct().ToList(), ct);

            foreach (var n in netted)
            {
                var tagIds = tagsByDocument.GetValueOrDefault(n.DocumentId) ?? [];
                var cell = (n.Date, n.CatalogItemId);
                if (!metricDeltaByCell.TryGetValue(cell, out var delta))
                    metricDeltaByCell[cell] = delta = new int[metrics.Count];

                for (var i = 0; i < metrics.Count; i++)
                {
                    // Removing a row from a metric's net removes it with the sign the direction gave it.
                    if (MetricCovers(metrics[i], document, cancelAction, cancelDirection, tagIds))
                        delta[i] -= Sign(cancelDirection) * n.Offset;

                    foreach (var (action, quantity) in n.Paired)
                        if (MetricCovers(metrics[i], document, action, baseDirection, tagIds))
                            delta[i] -= Sign(baseDirection) * quantity;
                }
            }
        }

        return new SameDayNetting(offsetByCell, offsetByItem, metricDeltaByCell);
    }

    /// <summary>Which move a document's cancellation reverses, and the action that reverses it.</summary>
    private static (StockMovementDirection Base, StockMovementDirection Cancel, string CancelAction) Shape(
        NettedDocument document) =>
        document switch
        {
            NettedDocument.Receipt => (StockMovementDirection.In, StockMovementDirection.Out,
                InventoryActions.CancelledPlacement),
            _ => (StockMovementDirection.Out, StockMovementDirection.In, InventoryActions.CancelledFulfillment),
        };

    private static int Sign(StockMovementDirection direction) =>
        direction is StockMovementDirection.In or StockMovementDirection.TransferIn ? 1 : -1;

    /// <summary>
    /// The day's moves and cancellations of one document type, per document, item, direction and action.
    /// Both branches project the same shape, so the aggregation below them is written once.
    /// </summary>
    private static async Task<List<DocumentDayRow>> FetchDocumentRowsAsync(
        IQueryable<StockMovement> query,
        IReadOnlyCollection<Guid> columnIds,
        NettedDocument document,
        int offsetMinutes,
        CancellationToken ct)
    {
        var (baseDirection, cancelDirection, cancelAction) = Shape(document);
        var scoped = query.Where(m => columnIds.Contains(m.CatalogItemId))
            .Where(m => m.Direction == baseDirection || (m.Direction == cancelDirection && m.Action == cancelAction));

        var keyed = document switch
        {
            NettedDocument.Receipt => scoped.Where(m => m.ReceiptId != null).Select(m => new
            {
                DocumentId = m.ReceiptId!.Value, m.CreatedAt, m.CatalogItemId, m.Direction, m.Action, m.Quantity,
            }),
            _ => scoped.Where(m => m.OrderId != null).Select(m => new
            {
                DocumentId = m.OrderId!.Value, m.CreatedAt, m.CatalogItemId, m.Direction, m.Action, m.Quantity,
            }),
        };

        return await keyed
            .GroupBy(x => new
            {
                Date = x.CreatedAt.AddMinutes(offsetMinutes).Date, x.CatalogItemId, x.DocumentId, x.Direction, x.Action,
            })
            .Select(g => new DocumentDayRow
            {
                Date = g.Key.Date,
                CatalogItemId = g.Key.CatalogItemId,
                DocumentId = g.Key.DocumentId,
                Direction = g.Key.Direction,
                Action = g.Key.Action,
                Quantity = g.Sum(x => x.Quantity),
                FirstAt = g.Min(x => x.CreatedAt),
                LastAt = g.Max(x => x.CreatedAt),
            })
            .ToListAsync(ct);
    }

    /// <summary>Tags of the netted documents, loaded only when some metric filters on that document's tags.</summary>
    private async Task<Dictionary<Guid, IReadOnlyCollection<Guid>>> LoadDocumentTagsAsync(
        NettedDocument document,
        IReadOnlyList<StockMovementMetricDto> metrics,
        IReadOnlyCollection<Guid> documentIds,
        CancellationToken ct)
    {
        if (!metrics.Any(m => OwnTagIds(m, document) is { Length: > 0 })) return [];

        var tagged = document switch
        {
            NettedDocument.Receipt => db.Receipts
                .Where(r => documentIds.Contains(r.Id))
                .Select(r => new { r.Id, TagIds = r.Tags.Select(t => t.Id).ToList() }),
            _ => db.Orders
                .Where(o => documentIds.Contains(o.Id))
                .Select(o => new { o.Id, TagIds = o.Tags.Select(t => t.Id).ToList() }),
        };

        return await tagged.ToDictionaryAsync(x => x.Id, x => (IReadOnlyCollection<Guid>)x.TagIds, ct);
    }

    private static Guid[]? OwnTagIds(StockMovementMetricDto metric, NettedDocument document) =>
        document == NettedDocument.Receipt ? metric.ReceiptTagIds : metric.OrderTagIds;

    /// <summary>
    /// Whether a metric's predicate keeps a movement of <paramref name="document"/> with these attributes.
    /// Mirrors <see cref="ApplyMetric"/>; a filter on any other document's tags never matches, because a
    /// movement carries at most one document.
    /// </summary>
    private static bool MetricCovers(
        StockMovementMetricDto metric,
        NettedDocument document,
        string action,
        StockMovementDirection direction,
        IReadOnlyCollection<Guid> documentTagIds)
    {
        if (metric.Actions is { Length: > 0 } actions && !actions.Contains(action)) return false;
        if (metric.Directions is { Length: > 0 } directions && !directions.Contains(direction)) return false;
        if (metric.WriteoffTagIds is { Length: > 0 } || metric.StocktakeTagIds is { Length: > 0 }) return false;

        var foreignTagIds = OwnTagIds(metric, document == NettedDocument.Receipt
            ? NettedDocument.Order
            : NettedDocument.Receipt);
        if (foreignTagIds is { Length: > 0 }) return false;

        return OwnTagIds(metric, document) is not { Length: > 0 } tagIds || tagIds.Any(documentTagIds.Contains);
    }

    /// <summary>
    /// The metric's own predicate, applied on top of the report filter. Same clauses as the report filter
    /// in <see cref="BuildAsync"/>, because a metric is a filter that produces a column instead of a page.
    /// </summary>
    private static IQueryable<StockMovement> ApplyMetric(
        IQueryable<StockMovement> query,
        StockMovementMetricDto metric)
    {
        if (metric.Actions is { Length: > 0 } actions)
            query = query.Where(m => actions.Contains(m.Action));

        if (metric.Directions is { Length: > 0 } directions)
            query = query.Where(m => directions.Contains(m.Direction));

        if (metric.ReceiptTagIds is { Length: > 0 } receiptTagIds)
            query = query.Where(m => m.Receipt != null && m.Receipt.Tags.Any(t => receiptTagIds.Contains(t.Id)));

        if (metric.OrderTagIds is { Length: > 0 } orderTagIds)
            query = query.Where(m => m.Order != null && m.Order.Tags.Any(t => orderTagIds.Contains(t.Id)));

        if (metric.WriteoffTagIds is { Length: > 0 } writeoffTagIds)
            query = query.Where(m => m.Writeoff != null && m.Writeoff.Tags.Any(t => writeoffTagIds.Contains(t.Id)));

        if (metric.StocktakeTagIds is { Length: > 0 } stocktakeTagIds)
            query = query.Where(m => m.Stocktake != null && m.Stocktake.Tags.Any(t => stocktakeTagIds.Contains(t.Id)));

        return query;
    }

    private static int[] Zeros(int count) => count == 0 ? [] : new int[count];

    private static int[] SumMetrics(IEnumerable<IReadOnlyList<int>> parts, int count)
    {
        var sum = Zeros(count);
        foreach (var part in parts)
            for (var i = 0; i < count && i < part.Count; i++)
                sum[i] += part[i];
        return sum;
    }

    private static StockMovementPivotTotalsDto SumCells(
        IReadOnlyList<StockMovementPivotCellDto> cells, int metricCount) =>
        new()
        {
            InQuantity = cells.Sum(c => c.InQuantity),
            OutQuantity = cells.Sum(c => c.OutQuantity),
            TransferInQuantity = cells.Sum(c => c.TransferInQuantity),
            TransferOutQuantity = cells.Sum(c => c.TransferOutQuantity),
            MovementsCount = cells.Sum(c => c.MovementsCount),
            Metrics = SumMetrics(cells.Select(c => c.Metrics), metricCount),
        };

    private static StockMovementPivotTotalsDto SumTotals(
        IEnumerable<StockMovementPivotTotalsDto> totals, int metricCount)
    {
        var list = totals.ToList();
        return new StockMovementPivotTotalsDto
        {
            InQuantity = list.Sum(t => t.InQuantity),
            OutQuantity = list.Sum(t => t.OutQuantity),
            TransferInQuantity = list.Sum(t => t.TransferInQuantity),
            TransferOutQuantity = list.Sum(t => t.TransferOutQuantity),
            MovementsCount = list.Sum(t => t.MovementsCount),
            Metrics = SumMetrics(list.Select(t => t.Metrics), metricCount),
        };
    }

    public async Task<StockMovementCellDto> GetCellAsync(
        ClaimsPrincipal user,
        StockMovementCellRequest request,
        CancellationToken ct = default)
    {
        var scope = await BuildAsync(user, request, ct);
        var query = request.Metric is null ? scope.Query : ApplyMetric(scope.Query, request.Metric);

        var older = request.Before is { } before ? query.Where(m => m.CreatedAt < before) : query;
        var cut = await FindPageCutAsync(older, ct);
        var page = cut is { } c ? older.Where(m => m.CreatedAt >= c) : older;

        var movements = await page
            .OrderBy(m => m.CreatedAt)
            .ThenBy(m => m.Id)
            .ProjectTo<StockMovementCellRowDto>(mapper.ConfigurationProvider)
            .ToListAsync(ct);

        await MarkNettedAsync(movements, scope.Query, request.Metric is null ? null : query, scope.OffsetMinutes, ct);

        return new StockMovementCellDto
        {
            Entries = GroupBursts(movements),
            TotalCount = request.Before is null ? await query.CountAsync(ct) : null,
            NextBefore = cut is { } next && await older.AnyAsync(m => m.CreatedAt < next, ct) ? next : null,
        };
    }

    /// <summary>
    /// Sets how much of each row the pivot's same-day netting takes out of the cell and attaches the other
    /// half of each pair that <paramref name="metricQuery"/> leaves out. Pairs are formed over
    /// <paramref name="reportQuery"/> exactly as <see cref="GetSameDayNettingAsync"/> forms them. That
    /// method works on per-action aggregates, so which rows a cancellation takes back is decided here, unit
    /// by unit: each cancellation, earliest first, against the moves made before it, latest first.
    /// </summary>
    private async Task MarkNettedAsync(
        List<StockMovementCellRowDto> movements,
        IQueryable<StockMovement> reportQuery,
        IQueryable<StockMovement>? metricQuery,
        int offsetMinutes,
        CancellationToken ct)
    {
        var orderIds = movements.Where(m => m.OrderId != null).Select(m => m.OrderId!.Value).Distinct().ToList();
        var receiptIds = movements.Where(m => m.ReceiptId != null).Select(m => m.ReceiptId!.Value).Distinct().ToList();
        if (orderIds.Count == 0 && receiptIds.Count == 0) return;

        var candidates = await reportQuery
            .Where(m => (m.OrderId != null && orderIds.Contains(m.OrderId.Value)) ||
                        (m.ReceiptId != null && receiptIds.Contains(m.ReceiptId.Value)))
            .ProjectTo<StockMovementCellRowDto>(mapper.ConfigurationProvider)
            .ToListAsync(ct);

        var links = new List<(StockMovementCellRowDto Cancellation, StockMovementCellRowDto Move, int Quantity)>();
        foreach (var document in Enum.GetValues<NettedDocument>())
        {
            var (baseDirection, cancelDirection, cancelAction) = Shape(document);
            var groups = candidates
                .Where(m => (document == NettedDocument.Receipt ? m.ReceiptId : m.OrderId) != null)
                .GroupBy(m => (
                    Day: m.CreatedAt.AddMinutes(offsetMinutes).Date,
                    m.CatalogItemId,
                    Document: document == NettedDocument.Receipt ? m.ReceiptId : m.OrderId));

            foreach (var g in groups)
            {
                var cancellations = g
                    .Where(m => m.Direction == cancelDirection && m.Action == cancelAction)
                    .OrderBy(m => m.CreatedAt)
                    .ThenBy(m => m.Id)
                    .ToList();
                if (cancellations.Count == 0) continue;

                var lastCancelledAt = cancellations[^1].CreatedAt;
                var moved = g
                    .Where(m => m.Direction == baseDirection && m.CreatedAt < lastCancelledAt)
                    .OrderByDescending(m => m.CreatedAt)
                    .ThenByDescending(m => m.Id)
                    .ToList();

                var offset = Math.Min(cancellations.Sum(m => m.Quantity), moved.Sum(m => m.Quantity));
                var cancelLeft = cancellations.ToDictionary(m => m.Id, m => m.Quantity);
                var moveLeft = moved.ToDictionary(m => m.Id, m => m.Quantity);

                void Link(StockMovementCellRowDto cancellation, StockMovementCellRowDto move)
                {
                    var take = Math.Min(offset, Math.Min(cancelLeft[cancellation.Id], moveLeft[move.Id]));
                    if (take == 0) return;
                    links.Add((cancellation, move, take));
                    offset -= take;
                    cancelLeft[cancellation.Id] -= take;
                    moveLeft[move.Id] -= take;
                }

                // Each cancellation first takes back the moves made before it, latest first — the order
                // things actually happened in.
                foreach (var cancellation in cancellations)
                    foreach (var move in moved.Where(m => m.CreatedAt < cancellation.CreatedAt))
                        Link(cancellation, move);

                // The pivot's rule is coarser — any move before the day's last cancellation — so whatever it
                // nets beyond the chronological pairs is matched regardless of order to keep the totals equal.
                foreach (var cancellation in cancellations)
                    foreach (var move in moved)
                        Link(cancellation, move);
            }
        }

        if (links.Count == 0) return;

        var nettedById = new Dictionary<Guid, int>();
        foreach (var (cancellation, move, quantity) in links)
        {
            nettedById[cancellation.Id] = nettedById.GetValueOrDefault(cancellation.Id) + quantity;
            nettedById[move.Id] = nettedById.GetValueOrDefault(move.Id) + quantity;
        }

        var linkedIds = nettedById.Keys.ToList();
        var coveredIds = metricQuery is null
            ? linkedIds.ToHashSet()
            : (await metricQuery.Where(m => linkedIds.Contains(m.Id)).Select(m => m.Id).ToListAsync(ct)).ToHashSet();

        // A half the metric leaves out hangs under exactly one listed partner — the one it shares the most
        // quantity with — so it is shown once however many rows it was matched against or pages they span.
        var counterpartsByOwner = links
            .SelectMany(l => new[] { (Half: l.Cancellation, Partner: l.Move, l.Quantity), (Half: l.Move, Partner: l.Cancellation, l.Quantity) })
            .Where(x => !coveredIds.Contains(x.Half.Id) && coveredIds.Contains(x.Partner.Id))
            .GroupBy(x => x.Half.Id)
            .Select(g => g.OrderByDescending(x => x.Quantity).ThenBy(x => x.Partner.CreatedAt).ThenBy(x => x.Partner.Id).First())
            .GroupBy(x => x.Partner.Id)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Half).OrderByDescending(h => h.CreatedAt).ToList());

        foreach (var half in candidates)
            half.NettedQuantity = nettedById.GetValueOrDefault(half.Id);

        foreach (var row in movements)
        {
            row.NettedQuantity = nettedById.GetValueOrDefault(row.Id);
            row.Counterparts = counterpartsByOwner.GetValueOrDefault(row.Id) ?? [];
        }
    }

    /// <summary>
    /// Where the page starting at the newest movement of <paramref name="older"/> ends, or null when the
    /// rest fits in one page. The cut lands on a pause longer than <see cref="GroupGap"/>, which no burst
    /// can span, so a group is never split between pages. A stream with no such pause for
    /// <see cref="MaxCutLookback"/> is cut at the end of that window anyway, and the burst running there
    /// may then be split.
    /// </summary>
    private static async Task<DateTime?> FindPageCutAsync(IQueryable<StockMovement> older, CancellationToken ct)
    {
        var newest = await older
            .OrderByDescending(m => m.CreatedAt)
            .Select(m => m.CreatedAt)
            .Skip(CellPageSize)
            .Take(1)
            .ToListAsync(ct);
        if (newest.Count == 0) return null;

        var cut = newest[0];
        var windowStart = cut - MaxCutLookback;
        var preceding = await older
            .Where(m => m.CreatedAt < cut && m.CreatedAt >= windowStart)
            .OrderByDescending(m => m.CreatedAt)
            .Select(m => m.CreatedAt)
            .ToListAsync(ct);

        // Running off the list without a break leaves the earliest movement read as the cut — either the
        // pause before it lies past the window's start, or there was none for the whole lookback.
        foreach (var at in preceding)
        {
            if (cut - at > GroupGap) break;
            cut = at;
        }

        return cut;
    }

    /// <summary>
    /// Chains each user's movements of one action and direction while the pause between neighbours stays
    /// within <see cref="GroupGap"/>; a chain longer than <see cref="GroupThreshold"/> becomes one entry.
    /// Chains of different keys may interleave. Movements without a user are never grouped.
    /// </summary>
    private static List<StockMovementEntryDto> GroupBursts(IReadOnlyList<StockMovementCellRowDto> oldestFirst)
    {
        var chains = new List<List<StockMovementCellRowDto>>();
        var open = new Dictionary<(Guid, string, StockMovementDirection), List<StockMovementCellRowDto>>();

        foreach (var m in oldestFirst)
        {
            if (m.UserId is { } userId)
            {
                var key = (userId, m.Action, m.Direction);
                if (open.TryGetValue(key, out var chain) && m.CreatedAt - chain[^1].CreatedAt <= GroupGap)
                {
                    chain.Add(m);
                    continue;
                }

                open[key] = chain = [m];
                chains.Add(chain);
            }
            else
            {
                chains.Add([m]);
            }
        }

        var entries = new List<StockMovementEntryDto>();
        foreach (var chain in chains)
        {
            if (chain.Count > GroupThreshold)
                entries.Add(ToEntry(chain, isGroup: true));
            else
                entries.AddRange(chain.Select(m => ToEntry([m], isGroup: false)));
        }

        return entries.OrderByDescending(e => e.LastAt).ToList();
    }

    private static StockMovementEntryDto ToEntry(List<StockMovementCellRowDto> oldestFirst, bool isGroup)
    {
        var first = oldestFirst[0];
        return new StockMovementEntryDto
        {
            IsGroup = isGroup,
            FirstAt = first.CreatedAt,
            LastAt = oldestFirst[^1].CreatedAt,
            Direction = first.Direction,
            Action = first.Action,
            UserId = first.UserId,
            UserName = first.UserName,
            Quantity = oldestFirst.Sum(m => m.Quantity),
            Movements = Enumerable.Reverse(oldestFirst).ToList(),
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
