using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using ProjectWarehouse.Server.Data;
using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Infrastructure;
using ProjectWarehouse.Server.Models;
using ProjectWarehouse.Server.Models.Analytics;

namespace ProjectWarehouse.Server.Services;

public class AnalyticsAbcService(
    ApplicationDbContext db,
    AnalyticsQueries queries,
    IAnalyticsSettingsService settings) : IAnalyticsAbcService
{
    /// <summary>Shop sales of one item in one currency on one day; null item and currency still carry units.</summary>
    private sealed class LineDay
    {
        public Guid? CatalogItemId { get; init; }
        public string? CurrencyCode { get; init; }
        public DateTime Day { get; init; }
        public int Units { get; init; }
        public int Lines { get; init; }
        public int AccruedLines { get; init; }
        public decimal Price { get; init; }
        public decimal Payout { get; init; }
    }

    private sealed class ItemDay
    {
        public Guid CatalogItemId { get; init; }
        public DateTime Day { get; init; }
        public int Units { get; init; }
    }

    /// <param name="Lines">Shop lines, unlinked ones included.</param>
    /// <param name="UnitDays">Units of linked shop lines and, on the units basis, of Direct orders.</param>
    /// <param name="Currencies">Currencies of the linked lines, the most frequent first.</param>
    private sealed record Sales(List<LineDay> Lines, List<ItemDay> UnitDays, List<string> Currencies);

    private sealed record AnalysedItem(int Rank, AbcRankedItem Item, double? Cv, int? Intervals, XyzClass? Xyz);

    private sealed record Analysis(
        AnalyticsPeriod Period,
        AnalyticsOptions Options,
        List<Guid> AccountIds,
        Sales Sales,
        string? Currency,
        IReadOnlyList<AbcRankedItem> Ranked,
        int XyzIntervals,
        List<AnalysedItem> Items);

    public async Task<AbcDto> GetAbcAsync(ClaimsPrincipal user, AbcRequest request, CancellationToken ct = default)
    {
        var (period, options, _, sales, currency, ranked, xyzIntervals, analysed) = await AnalyseAsync(request, ct);

        var filtered = await FilterAsync(request, analysed, ct);
        var page = filtered.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToList();
        var names = await LoadNamesAsync(page, ct);

        var totalValue = ranked.Sum(r => r.Value);
        var currencyLines = sales.Lines.Where(l => l.CurrencyCode == currency).ToList();

        return new AbcDto
        {
            From = period.From,
            To = period.To,
            TimeZoneId = period.TimeZoneId,
            Basis = request.Basis,
            CurrencyCode = currency,
            Currencies = sales.Currencies,
            PayoutCoverage = request.Basis == AnalyticsAbcBasis.Payout
                ? AnalyticsCalculator.Ratio(currencyLines.Sum(l => l.AccruedLines), currencyLines.Sum(l => l.Lines))
                : null,
            UnlinkedLines = sales.Lines.Where(l => l.CatalogItemId == null).Sum(l => l.Lines),
            TotalValue = totalValue,
            Settings = AppliedSettings(options),
            XyzIntervals = xyzIntervals,
            XyzFromFirstSale = request.XyzFromFirstSale,
            Classes = Enum.GetValues<AbcClass>()
                .Select(c =>
                {
                    var members = ranked.Where(r => r.Class == c).ToList();
                    var value = members.Sum(r => r.Value);
                    return new AbcClassSummaryDto
                    {
                        Class = c,
                        Items = members.Count,
                        ItemsShare = AnalyticsCalculator.Ratio(members.Count, ranked.Count) ?? 0,
                        Value = value,
                        ValueShare = AnalyticsCalculator.Ratio(value, totalValue) ?? 0,
                    };
                })
                .ToList(),
            Matrix = analysed
                .GroupBy(a => (a.Item.Class, a.Xyz))
                .OrderBy(g => g.Key.Class)
                .ThenBy(g => g.Key.Xyz == null)
                .ThenBy(g => g.Key.Xyz)
                .Select(g => new AbcMatrixCellDto { AbcClass = g.Key.Class, XyzClass = g.Key.Xyz, Items = g.Count() })
                .ToList(),
            Pareto = ranked.Select(r => new AbcParetoPointDto { Value = r.Value, Class = r.Class }).ToList(),
            Items = new Paginated<AbcItemDto>
            {
                Total = filtered.Count,
                Page = request.Page,
                PageSize = request.PageSize,
                Items = page
                    .Select(a => new AbcItemDto
                    {
                        Rank = a.Rank,
                        CatalogItemId = a.Item.Id,
                        Name = names[a.Item.Id].FullName,
                        Type = names[a.Item.Id].Type,
                        Value = a.Item.Value,
                        Share = a.Item.Share,
                        CumulativeShare = a.Item.CumulativeShare,
                        AbcClass = a.Item.Class,
                        XyzClass = a.Xyz,
                        XyzIntervals = a.Intervals,
                        Cv = a.Cv,
                    })
                    .ToList(),
            },
        };
    }

    public async Task<AbcTimelineDto> GetTimelineAsync(
        ClaimsPrincipal user, AbcFilterRequest request, CancellationToken ct = default)
    {
        var (period, options, accountIds, _, currency, _, _, analysed) = await AnalyseAsync(request, ct);
        var rows = await FilterAsync(request, analysed, ct);

        var windows = AnalyticsCalculator.TimelineWindows(period.To, period.Today);
        var offset = TimeSpan.FromMinutes(period.OffsetMinutes);
        var rangeFromUtc = AnalyticsQueries.ToUtc(windows[0].From, offset);
        var sales = rows.Count == 0
            ? new Sales([], [], [])
            : await LoadSalesAsync(request, accountIds, rangeFromUtc,
                AnalyticsQueries.ToUtc(windows[^1].To.AddDays(1), offset), period.OffsetMinutes, ct);

        var dayUnits = DayUnits(sales, rows.Select(r => r.Item.Id).ToHashSet());
        var launches = request.XyzFromFirstSale
            ? await LoadLaunchesAsync(request, accountIds, dayUnits, rangeFromUtc, ct)
            : [];

        var cells = rows.ToDictionary(r => r.Item.Id, _ => new List<AbcTimelineCellDto>(windows.Count));
        var months = new List<AbcTimelineMonthDto>(windows.Count);
        foreach (var window in windows)
        {
            // Every item sold in the window is ranked, not only the rows, so a row's class matches a standalone analysis
            var ranked = Rank(request, options, sales, currency, window.From, window.To).ToDictionary(r => r.Id);
            var intervals = AnalyticsCalculator.FullIntervals(window.From, window.To, options.XyzStep, period.Today);
            var xyzAvailable = intervals.Count >= options.XyzMinIntervals;
            months.Add(new AbcTimelineMonthDto
            {
                Month = window.Month,
                From = window.From,
                To = window.To,
                IsPartial = window.IsPartial,
                XyzIntervals = intervals.Count,
            });

            foreach (var row in rows)
            {
                var id = row.Item.Id;
                if (!ranked.TryGetValue(id, out var item))
                {
                    cells[id].Add(new AbcTimelineCellDto());
                    continue;
                }

                var cv = xyzAvailable
                    ? Xyz(intervals, dayUnits.GetValueOrDefault(id) ?? [],
                        launches.TryGetValue(id, out var launch) ? launch : (DateOnly?)null, options,
                        period.Today).Cv
                    : null;
                cells[id].Add(new AbcTimelineCellDto
                {
                    AbcClass = item.Class,
                    Share = item.Share,
                    XyzClass = AnalyticsCalculator.ClassifyXyz(cv, options.XyzBoundaryX, options.XyzBoundaryY),
                    Cv = cv,
                });
            }
        }

        var names = await LoadNamesAsync(rows, ct);

        return new AbcTimelineDto
        {
            TimeZoneId = period.TimeZoneId,
            Basis = request.Basis,
            CurrencyCode = currency,
            WindowDays = AnalyticsCalculator.TimelineWindowDays,
            XyzFromFirstSale = request.XyzFromFirstSale,
            Settings = AppliedSettings(options),
            Months = months,
            Rows = rows
                .Select(r => new AbcTimelineRowDto
                {
                    Rank = r.Rank,
                    CatalogItemId = r.Item.Id,
                    Name = names[r.Item.Id].FullName,
                    Type = names[r.Item.Id].Type,
                    Cells = cells[r.Item.Id],
                })
                .ToList(),
        };
    }

    private async Task<Analysis> AnalyseAsync(AbcFilterRequest request, CancellationToken ct)
    {
        var period = await queries.ResolvePeriodAsync(request, ct);
        var options = await settings.GetOptionsAsync(ct);
        var accounts = await queries.LoadAccountsAsync(request, ct);
        var accountIds = accounts.Select(a => a.Id).ToList();

        var sales = await LoadSalesAsync(request, accountIds, period.FromUtc, period.ToUtc, period.OffsetMinutes, ct);
        var currency = request.Basis == AnalyticsAbcBasis.Units ? null
            : request.CurrencyCode != null && sales.Currencies.Contains(request.CurrencyCode) ? request.CurrencyCode
            : sales.Currencies.FirstOrDefault();
        var ranked = Rank(request, options, sales, currency, period.From, period.To);

        var intervals = AnalyticsCalculator.FullIntervals(period.From, period.To, options.XyzStep, period.Today);
        var xyzAvailable = intervals.Count >= options.XyzMinIntervals;
        var xyz = new Dictionary<Guid, (double? Cv, int Intervals)>();
        if (xyzAvailable)
        {
            var dayUnits = DayUnits(sales, ranked.Select(r => r.Id).ToHashSet());
            var launches = request.XyzFromFirstSale
                ? await LoadLaunchesAsync(request, accountIds, dayUnits, period.FromUtc, ct)
                : [];

            foreach (var item in ranked)
                xyz[item.Id] = Xyz(intervals, dayUnits.GetValueOrDefault(item.Id) ?? [],
                    launches.TryGetValue(item.Id, out var launch) ? launch : (DateOnly?)null, options, period.Today);
        }

        var analysed = ranked
            .Select((r, i) =>
            {
                var (cv, itemIntervals) = xyz.GetValueOrDefault(r.Id);
                return new AnalysedItem(i + 1, r, cv, xyzAvailable ? itemIntervals : null,
                    AnalyticsCalculator.ClassifyXyz(cv, options.XyzBoundaryX, options.XyzBoundaryY));
            })
            .ToList();

        return new Analysis(period, options, accountIds, sales, currency, ranked, intervals.Count, analysed);
    }

    private async Task<Sales> LoadSalesAsync(
        AbcFilterRequest request,
        List<Guid> accountIds,
        DateTime fromUtc,
        DateTime toUtc,
        int offset,
        CancellationToken ct)
    {
        var lines = accountIds.Count == 0
            ? []
            : await queries
                .SaleLines(accountIds, fromUtc, toUtc)
                // Accrual is part of the key: a navigation read inside an aggregate becomes a correlated subquery
                // per group, and with a group per item and day that rescans the whole period thousands of times.
                // A day of an item may therefore come as two rows; every consumer below only sums them.
                .GroupBy(i => new
                {
                    i.CatalogItemId,
                    i.CurrencyCode,
                    Day = i.Order.EffectiveDate.AddMinutes(offset).Date,
                    Accrued = i.Order.MarketplaceOrder!.Status == MarketplaceOrderStatus.Delivered && i.Payout > 0,
                })
                .Select(g => new LineDay
                {
                    CatalogItemId = g.Key.CatalogItemId,
                    CurrencyCode = g.Key.CurrencyCode,
                    Day = g.Key.Day,
                    Units = g.Sum(i => i.Quantity),
                    Lines = g.Count(),
                    AccruedLines = g.Key.Accrued ? g.Count() : 0,
                    Price = g.Sum(i => i.Price != null ? i.Price.Value * i.Quantity : 0),
                    Payout = g.Key.Accrued ? g.Sum(i => i.Payout ?? 0) : 0,
                })
                .ToListAsync(ct);

        // Money exists on shop lines only, so a money basis analyses the shops alone, XYZ included
        var directDays = request.IncludeDirect && request.Basis == AnalyticsAbcBasis.Units
            ? await queries
                .DirectOrders(request.DirectTagIds, AnalyticsQueries.DirectSaleStatuses, fromUtc, toUtc)
                .SelectMany(o => o.Boxes)
                .SelectMany(b => b.Components)
                .GroupBy(c => new { c.CatalogItemId, Day = c.OrderBox.Order.EffectiveDate.AddMinutes(offset).Date })
                .Select(g => new ItemDay
                {
                    CatalogItemId = g.Key.CatalogItemId, Day = g.Key.Day, Units = g.Sum(c => c.Quantity),
                })
                .ToListAsync(ct)
            : [];

        var linked = lines.Where(l => l.CatalogItemId != null).ToList();
        var currencies = linked
            .Where(l => l.CurrencyCode != null)
            .GroupBy(l => l.CurrencyCode!)
            .OrderByDescending(g => g.Sum(l => l.Lines))
            .ThenBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => g.Key)
            .ToList();
        var unitDays = linked
            .Select(l => new ItemDay { CatalogItemId = l.CatalogItemId!.Value, Day = l.Day, Units = l.Units })
            .Concat(directDays)
            .ToList();

        return new Sales(lines, unitDays, currencies);
    }

    /// <summary>ABC over the loaded days from <paramref name="from"/> to <paramref name="to"/> inclusive.</summary>
    private static IReadOnlyList<AbcRankedItem> Rank(
        AbcFilterRequest request,
        AnalyticsOptions options,
        Sales sales,
        string? currency,
        DateOnly from,
        DateOnly to)
    {
        if (request.Basis != AnalyticsAbcBasis.Units && currency == null) return [];

        var fromDay = from.ToDateTime(TimeOnly.MinValue);
        var toDay = to.ToDateTime(TimeOnly.MinValue);
        var values = request.Basis switch
        {
            AnalyticsAbcBasis.Units => sales.UnitDays
                .Where(d => d.Day >= fromDay && d.Day <= toDay)
                .GroupBy(d => d.CatalogItemId)
                .Select(g => (Id: g.Key, Value: (decimal)g.Sum(d => d.Units))),
            _ => sales.Lines
                .Where(l => l.CatalogItemId != null && l.CurrencyCode == currency && l.Day >= fromDay && l.Day <= toDay)
                .GroupBy(l => l.CatalogItemId!.Value)
                .Select(g => (Id: g.Key,
                    Value: g.Sum(l => request.Basis == AnalyticsAbcBasis.Payout ? l.Payout : l.Price))),
        };

        return AnalyticsCalculator.RankAbc(values, options.AbcBoundaryA, options.AbcBoundaryB);
    }

    private static Dictionary<Guid, List<(DateOnly Day, int Units)>> DayUnits(Sales sales, HashSet<Guid> ids) =>
        sales.UnitDays
            .Where(d => ids.Contains(d.CatalogItemId))
            .GroupBy(d => d.CatalogItemId)
            .ToDictionary(g => g.Key, g => g.Select(d => (DateOnly.FromDateTime(d.Day), d.Units)).ToList());

    /// <param name="intervals">The full intervals of the analysed span.</param>
    /// <param name="days">The item's units per day.</param>
    /// <param name="launch">First sale ever when it falls inside the loaded days; null when the item sold before them.</param>
    /// <param name="options">Applied settings.</param>
    /// <param name="today">The caller's today.</param>
    private static (double? Cv, int Intervals) Xyz(
        IReadOnlyList<AnalyticsInterval> intervals,
        List<(DateOnly Day, int Units)> days,
        DateOnly? launch,
        AnalyticsOptions options,
        DateOnly today)
    {
        var itemIntervals = launch is { } firstSale
            ? AnalyticsCalculator.IntervalsSince(intervals, firstSale)
            : intervals;
        var cv = itemIntervals.Count >= options.XyzMinIntervals
            ? AnalyticsCalculator.CoefficientOfVariation(AnalyticsCalculator
                .SumByInterval(itemIntervals, today, days)
                .Select(v => v ?? 0)
                .ToList())
            : null;
        return (cv, itemIntervals.Count);
    }

    /// <summary>
    /// First sale of each item of <paramref name="dayUnits"/> that never sold before <paramref name="loadedFromUtc"/>
    /// over the same channels. An item sold before then has no launch inside the loaded days: its quiet weeks are
    /// real zero demand.
    /// </summary>
    private async Task<Dictionary<Guid, DateOnly>> LoadLaunchesAsync(
        AbcFilterRequest request,
        List<Guid> accountIds,
        Dictionary<Guid, List<(DateOnly Day, int Units)>> dayUnits,
        DateTime loadedFromUtc,
        CancellationToken ct)
    {
        if (dayUnits.Count == 0) return [];

        var soldBefore = await LoadSoldBeforeAsync(request, accountIds, dayUnits.Keys.ToList(), loadedFromUtc, ct);
        return dayUnits
            .Where(p => !soldBefore.Contains(p.Key) && p.Value.Count > 0)
            .ToDictionary(p => p.Key, p => p.Value.Min(d => d.Day));
    }

    /// <summary>
    /// The items that already sold before <paramref name="beforeUtc"/> over the same channels. A distinct list
    /// rather than a first-sale date per item: a <c>Min</c> over the order's date would read a navigation inside
    /// an aggregate, and that becomes a subquery per item scanning the whole history.
    /// </summary>
    private async Task<HashSet<Guid>> LoadSoldBeforeAsync(
        AbcFilterRequest request,
        List<Guid> accountIds,
        List<Guid> ids,
        DateTime beforeUtc,
        CancellationToken ct)
    {
        var result = new HashSet<Guid>();

        if (accountIds.Count > 0)
            result.UnionWith(await queries
                .SaleLines(accountIds, DateTime.UnixEpoch, beforeUtc)
                .Where(i => i.CatalogItemId != null && ids.Contains(i.CatalogItemId.Value))
                .Select(i => i.CatalogItemId!.Value)
                .Distinct()
                .ToListAsync(ct));

        if (request.IncludeDirect && request.Basis == AnalyticsAbcBasis.Units)
            result.UnionWith(await queries
                .DirectOrders(request.DirectTagIds, AnalyticsQueries.DirectSaleStatuses, DateTime.UnixEpoch, beforeUtc)
                .SelectMany(o => o.Boxes)
                .SelectMany(b => b.Components)
                .Where(c => ids.Contains(c.CatalogItemId))
                .Select(c => c.CatalogItemId)
                .Distinct()
                .ToListAsync(ct));

        return result;
    }

    /// <summary>The class filters and the search, keeping rank order.</summary>
    private async Task<List<AnalysedItem>> FilterAsync(
        AbcFilterRequest request, List<AnalysedItem> items, CancellationToken ct)
    {
        var filtered = items
            .Where(a => request.AbcClass == null || a.Item.Class == request.AbcClass)
            .Where(a => request.XyzClass == null || a.Xyz == request.XyzClass)
            .ToList();
        if (string.IsNullOrWhiteSpace(request.SearchString)) return filtered;

        var candidateIds = filtered.Select(a => a.Item.Id).ToList();
        var matched = (await db.CatalogItems
                .Where(c => candidateIds.Contains(c.Id))
                .WhereMatchesSearch(c => c.SearchString, request.SearchString)
                .Select(c => c.Id)
                .ToListAsync(ct))
            .ToHashSet();
        return filtered.Where(a => matched.Contains(a.Item.Id)).ToList();
    }

    private async Task<Dictionary<Guid, (string FullName, CatalogItemType Type)>> LoadNamesAsync(
        List<AnalysedItem> items, CancellationToken ct)
    {
        var ids = items.Select(a => a.Item.Id).ToList();
        return await db.CatalogItems
            .Where(c => ids.Contains(c.Id))
            .Select(c => new { c.Id, c.FullName, c.Type })
            .ToDictionaryAsync(c => c.Id, c => (c.FullName, c.Type), ct);
    }

    private static AbcAppliedSettingsDto AppliedSettings(AnalyticsOptions options) => new()
    {
        AbcBoundaryA = options.AbcBoundaryA,
        AbcBoundaryB = options.AbcBoundaryB,
        XyzBoundaryX = options.XyzBoundaryX,
        XyzBoundaryY = options.XyzBoundaryY,
        XyzStep = options.XyzStep,
        XyzMinIntervals = options.XyzMinIntervals,
    };
}
