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

    public async Task<AbcDto> GetAbcAsync(ClaimsPrincipal user, AbcRequest request, CancellationToken ct = default)
    {
        var period = await queries.ResolvePeriodAsync(request, ct);
        var options = await settings.GetOptionsAsync(ct);
        var accounts = await queries.LoadAccountsAsync(request, ct);
        var accountIds = accounts.Select(a => a.Id).ToList();
        var offset = period.OffsetMinutes;
        var moneyBasis = request.Basis != AnalyticsAbcBasis.Units;

        var lines = accountIds.Count == 0
            ? []
            : await queries
                .SaleLines(accountIds, period.FromUtc, period.ToUtc)
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
        var directDays = request.IncludeDirect && !moneyBasis
            ? await queries
                .DirectOrders(request.DirectTagIds, AnalyticsQueries.DirectSaleStatuses, period.FromUtc,
                    period.ToUtc)
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
        var currency = !moneyBasis ? null
            : request.CurrencyCode != null && currencies.Contains(request.CurrencyCode) ? request.CurrencyCode
            : currencies.FirstOrDefault();

        var unitDays = linked
            .Select(l => new ItemDay { CatalogItemId = l.CatalogItemId!.Value, Day = l.Day, Units = l.Units })
            .Concat(directDays)
            .ToList();

        var values = request.Basis switch
        {
            AnalyticsAbcBasis.Units => unitDays
                .GroupBy(d => d.CatalogItemId)
                .Select(g => (Id: g.Key, Value: (decimal)g.Sum(d => d.Units))),
            _ => linked
                .Where(l => l.CurrencyCode == currency)
                .GroupBy(l => l.CatalogItemId!.Value)
                .Select(g => (Id: g.Key,
                    Value: g.Sum(l => request.Basis == AnalyticsAbcBasis.Payout ? l.Payout : l.Price))),
        };
        IReadOnlyList<AbcRankedItem> ranked = currency == null && moneyBasis
            ? []
            : AnalyticsCalculator.RankAbc(values, options.AbcBoundaryA, options.AbcBoundaryB);

        var intervals = AnalyticsCalculator.FullIntervals(period.From, period.To, options.XyzStep, period.Today);
        var xyzAvailable = intervals.Count >= options.XyzMinIntervals;
        var xyz = new Dictionary<Guid, (double? Cv, int Intervals)>();
        if (xyzAvailable)
        {
            var dayUnits = unitDays
                .GroupBy(d => d.CatalogItemId)
                .ToDictionary(g => g.Key, g => g.Select(d => (Day: DateOnly.FromDateTime(d.Day), d.Units)).ToList());
            var soldBefore = request.XyzFromFirstSale
                ? await LoadSoldBeforeAsync(request, accountIds, !moneyBasis, ranked, period, ct)
                : [];

            foreach (var item in ranked)
            {
                var days = dayUnits.GetValueOrDefault(item.Id) ?? [];
                // An item sold before the period has no launch inside it: its quiet weeks are real zero demand
                var itemIntervals = request.XyzFromFirstSale && !soldBefore.Contains(item.Id) && days.Count > 0
                    ? AnalyticsCalculator.IntervalsSince(intervals, days.Min(d => d.Day))
                    : intervals;

                var cv = itemIntervals.Count >= options.XyzMinIntervals
                    ? AnalyticsCalculator.CoefficientOfVariation(AnalyticsCalculator
                        .SumByInterval(itemIntervals, period.Today, days)
                        .Select(v => v ?? 0)
                        .ToList())
                    : null;
                xyz[item.Id] = (cv, itemIntervals.Count);
            }
        }

        var analysed = ranked
            .Select((r, i) =>
            {
                var (cv, itemIntervals) = xyz.GetValueOrDefault(r.Id);
                return (Rank: i + 1, Item: r, Cv: cv, Intervals: xyzAvailable ? itemIntervals : (int?)null,
                    Xyz: AnalyticsCalculator.ClassifyXyz(cv, options.XyzBoundaryX, options.XyzBoundaryY));
            })
            .ToList();

        var filtered = analysed
            .Where(a => request.AbcClass == null || a.Item.Class == request.AbcClass)
            .Where(a => request.XyzClass == null || a.Xyz == request.XyzClass);
        if (!string.IsNullOrWhiteSpace(request.SearchString))
        {
            var candidateIds = filtered.Select(a => a.Item.Id).ToList();
            var matched = (await db.CatalogItems
                    .Where(c => candidateIds.Contains(c.Id))
                    .WhereMatchesSearch(c => c.SearchString, request.SearchString)
                    .Select(c => c.Id)
                    .ToListAsync(ct))
                .ToHashSet();
            filtered = filtered.Where(a => matched.Contains(a.Item.Id));
        }

        var filteredList = filtered.ToList();
        var page = filteredList.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToList();
        var pageIds = page.Select(a => a.Item.Id).ToList();
        var names = await db.CatalogItems
            .Where(c => pageIds.Contains(c.Id))
            .Select(c => new { c.Id, c.FullName, c.Type })
            .ToDictionaryAsync(c => c.Id, ct);

        var totalValue = ranked.Sum(r => r.Value);
        var currencyLines = lines.Where(l => l.CurrencyCode == currency).ToList();

        return new AbcDto
        {
            From = period.From,
            To = period.To,
            TimeZoneId = period.TimeZoneId,
            Basis = request.Basis,
            CurrencyCode = currency,
            Currencies = currencies,
            PayoutCoverage = request.Basis == AnalyticsAbcBasis.Payout
                ? AnalyticsCalculator.Ratio(currencyLines.Sum(l => l.AccruedLines), currencyLines.Sum(l => l.Lines))
                : null,
            UnlinkedLines = lines.Where(l => l.CatalogItemId == null).Sum(l => l.Lines),
            TotalValue = totalValue,
            Settings = new AbcAppliedSettingsDto
            {
                AbcBoundaryA = options.AbcBoundaryA,
                AbcBoundaryB = options.AbcBoundaryB,
                XyzBoundaryX = options.XyzBoundaryX,
                XyzBoundaryY = options.XyzBoundaryY,
                XyzStep = options.XyzStep,
                XyzMinIntervals = options.XyzMinIntervals,
            },
            XyzIntervals = intervals.Count,
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
                Total = filteredList.Count,
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

    /// <summary>
    /// The analysed items that already sold before the period over the same channels. A distinct list rather
    /// than a first-sale date per item: a <c>Min</c> over the order's date would read a navigation inside an
    /// aggregate, and that becomes a subquery per item scanning the whole history.
    /// </summary>
    private async Task<HashSet<Guid>> LoadSoldBeforeAsync(
        AbcRequest request,
        List<Guid> accountIds,
        bool includeDirect,
        IReadOnlyList<AbcRankedItem> ranked,
        AnalyticsPeriod period,
        CancellationToken ct)
    {
        var ids = ranked.Select(r => r.Id).ToList();
        var result = new HashSet<Guid>();

        if (accountIds.Count > 0)
            result.UnionWith(await queries
                .SaleLines(accountIds, DateTime.UnixEpoch, period.FromUtc)
                .Where(i => i.CatalogItemId != null && ids.Contains(i.CatalogItemId.Value))
                .Select(i => i.CatalogItemId!.Value)
                .Distinct()
                .ToListAsync(ct));

        if (includeDirect && request.IncludeDirect)
            result.UnionWith(await queries
                .DirectOrders(request.DirectTagIds, AnalyticsQueries.DirectSaleStatuses, DateTime.UnixEpoch,
                    period.FromUtc)
                .SelectMany(o => o.Boxes)
                .SelectMany(b => b.Components)
                .Where(c => ids.Contains(c.CatalogItemId))
                .Select(c => c.CatalogItemId)
                .Distinct()
                .ToListAsync(ct));

        return result;
    }
}
