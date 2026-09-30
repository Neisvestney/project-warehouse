using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using ProjectWarehouse.Server.Data;
using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Infrastructure;
using ProjectWarehouse.Server.Models.Analytics;

namespace ProjectWarehouse.Server.Services;

public class AnalyticsChannelsService(
    ApplicationDbContext db,
    AnalyticsQueries queries,
    IAnalyticsSettingsService settings) : IAnalyticsChannelsService
{
    private const int TopReasons = 10;

    /// <summary>Sales of one shop in one currency; a null currency still carries units.</summary>
    private sealed class LineAggregate
    {
        public Guid AccountId { get; init; }
        public string? CurrencyCode { get; init; }
        public int Units { get; init; }
        public int Lines { get; init; }
        public bool Accrued { get; init; }
        public decimal PriceRevenue { get; init; }
        public int PricedOrders { get; init; }
        public decimal DiscountPrice { get; init; }
        public decimal DiscountOldPrice { get; init; }
        public decimal DiscountAmount { get; init; }
    }

    /// <summary>Journal net of one shop's accrued sales in one currency.</summary>
    private sealed class PayoutAggregate
    {
        public Guid AccountId { get; init; }
        public string CurrencyCode { get; init; } = null!;
        public decimal Revenue { get; init; }

        /// <summary>Net of the sales not reversed in full — the average check's numerator.</summary>
        public decimal KeptRevenue { get; init; }

        public int KeptOrders { get; init; }
    }

    private sealed class DirectOrderRow
    {
        public bool IsCurrent { get; init; }
        public bool IsSale { get; init; }
        public int Units { get; init; }
        public List<Guid> TagIds { get; init; } = [];
    }

    /// <summary>One channel's value on one day; a null account is the Direct channel.</summary>
    private class DayValue
    {
        public Guid? AccountId { get; init; }
        public DateTime Day { get; init; }
        public int Value { get; init; }
    }

    private sealed class DirectOrderDay : DayValue
    {
        public List<Guid> TagIds { get; init; } = [];
    }

    /// <summary>Counted orders of one shop on one day, split into sales and cancellations.</summary>
    private sealed class OrderOutcomeDay : DayValue
    {
        public bool IsCancelled { get; init; }
    }

    public async Task<ChannelsSummaryDto> GetSummaryAsync(
        ClaimsPrincipal user, ChannelsSummaryRequest request, CancellationToken ct = default)
    {
        var period = await queries.ResolvePeriodAsync(request, ct);
        var options = await settings.GetOptionsAsync(ct);
        var accounts = await queries.LoadAccountsAsync(request, ct);
        var accountIds = accounts.Select(a => a.Id).ToList();

        var marketplaceRows = accountIds.Count == 0
            ? []
            : await BuildMarketplaceRowsAsync(accountIds, period, request.MoneyMode, ct);

        var directRows = request.IncludeDirect
            ? await BuildDirectRowsAsync(request.DirectTagIds, period, ct)
            : [];

        // Shares are taken among the selected channels; the Direct total counts once, its tag rows overlap
        var totalUnits = marketplaceRows.Values.Sum(r => r.Units)
            + directRows.Where(r => r.Kind == AnalyticsChannelKind.Direct).Sum(r => r.Units);
        var revenueByCurrency = marketplaceRows.Values
            .SelectMany(r => r.Money)
            .GroupBy(m => m.CurrencyCode)
            .ToDictionary(g => g.Key, g => g.Sum(m => m.Revenue));

        var rows = new List<ChannelSummaryRowDto>();
        foreach (var account in accounts)
        {
            var row = marketplaceRows.GetValueOrDefault(account.Id) ?? new ChannelSummaryRowDto();
            rows.Add(new ChannelSummaryRowDto
            {
                Kind = AnalyticsChannelKind.Marketplace,
                MarketplaceAccountId = account.Id,
                MarketplaceType = account.Type,
                Name = account.Name,
                Orders = row.Orders,
                PreviousOrders = row.PreviousOrders,
                Units = row.Units,
                Cancellations = row.Cancellations,
                CancellationRate = row.CancellationRate,
                ReturnedUnits = row.ReturnedUnits ?? 0,
                ReturnRate = row.ReturnRate,
                UnitsShare = AnalyticsCalculator.Ratio(row.Units, totalUnits),
                PayoutCoverage = request.MoneyMode == AnalyticsMoneyMode.Payout ? row.PayoutCoverage : null,
                Money = row.Money
                    .Select(m => new ChannelMoneyDto
                    {
                        CurrencyCode = m.CurrencyCode,
                        Revenue = m.Revenue,
                        AverageCheck = m.AverageCheck,
                        RevenueShare = AnalyticsCalculator.Ratio(m.Revenue, revenueByCurrency[m.CurrencyCode]),
                        DiscountDepth = m.DiscountDepth,
                        DiscountAmount = m.DiscountAmount,
                    })
                    .ToList(),
            });
        }

        rows.AddRange(directRows.Select(r => new ChannelSummaryRowDto
        {
            Kind = r.Kind,
            TagId = r.TagId,
            Name = r.Name,
            Orders = r.Orders,
            PreviousOrders = r.PreviousOrders,
            Units = r.Units,
            Cancellations = r.Cancellations,
            CancellationRate = r.CancellationRate,
            UnitsShare = AnalyticsCalculator.Ratio(r.Units, totalUnits),
        }));

        var (cancellations, topReasons) = accountIds.Count == 0
            ? ([], [])
            : await BuildCancellationsAsync(accountIds, period, ct);

        return new ChannelsSummaryDto
        {
            From = period.From,
            To = period.To,
            PreviousFrom = period.PreviousFrom,
            PreviousTo = period.PreviousTo,
            TimeZoneId = period.TimeZoneId,
            MoneyMode = request.MoneyMode,
            ReturnsMaturityDays = options.ReturnsMaturityDays,
            ReturnsImmature =
                AnalyticsCalculator.IsReturnsImmature(period.To, period.Today, options.ReturnsMaturityDays),
            Rows = rows,
            Cancellations = cancellations,
            TopCancelReasons = topReasons,
        };
    }

    public async Task<ChannelsTimeseriesDto> GetTimeseriesAsync(
        ClaimsPrincipal user, ChannelsTimeseriesRequest request, CancellationToken ct = default)
    {
        var period = await queries.ResolvePeriodAsync(request, ct);
        var step = request.Step ?? AnalyticsCalculator.DefaultStep(period.From, period.To);
        var intervals = AnalyticsCalculator.SplitIntervals(period.From, period.To, step);
        var accounts = await queries.LoadAccountsAsync(request, ct);
        var days = await LoadSaleDaysAsync(request, accounts, request.Measure, request.IncludeDirect, period, ct);

        var series = accounts
            .Select(a => Series(AnalyticsChannelKind.Marketplace, a, intervals, period.Today,
                days.Where(d => d.AccountId == a.Id)))
            .ToList();

        if (request.IncludeDirect)
            series.Add(Series(AnalyticsChannelKind.Direct, null, intervals, period.Today,
                days.Where(d => d.AccountId == null)));

        return new ChannelsTimeseriesDto
        {
            From = period.From,
            To = period.To,
            TimeZoneId = period.TimeZoneId,
            Step = step,
            Measure = request.Measure,
            Intervals = AnalyticsCalculator.ToIntervalDtos(intervals, period.Today),
            Series = series,
        };
    }

    public async Task<ChannelsReturnsDto> GetReturnsAsync(
        ClaimsPrincipal user, ChannelsReturnsRequest request, CancellationToken ct = default)
    {
        var period = await queries.ResolvePeriodAsync(request, ct);
        var options = await settings.GetOptionsAsync(ct);
        var step = request.Step ?? AnalyticsCalculator.DefaultStep(period.From, period.To);
        var intervals = AnalyticsCalculator.SplitIntervals(period.From, period.To, step);
        var accounts = await queries.LoadAccountsAsync(request, ct);
        var accountIds = accounts.Select(a => a.Id).ToList();

        var result = new ChannelsReturnsDto
        {
            From = period.From,
            To = period.To,
            TimeZoneId = period.TimeZoneId,
            MoneyMode = request.MoneyMode,
            ReturnsMaturityDays = options.ReturnsMaturityDays,
            ReturnsImmature =
                AnalyticsCalculator.IsReturnsImmature(period.To, period.Today, options.ReturnsMaturityDays),
            Step = step,
            Intervals = AnalyticsCalculator.ToIntervalDtos(intervals, period.Today),
        };

        if (accountIds.Count == 0) return result;

        var offset = period.OffsetMinutes;
        var cohort = queries.CohortReturns(accountIds, period.FromUtc, period.ToUtc);

        var soldUnits = await queries
            .SaleLines(accountIds, period.FromUtc, period.ToUtc)
            .GroupBy(i => i.Order.MarketplaceOrder!.MarketplaceAccountId)
            .Select(g => new { AccountId = g.Key, Units = g.Sum(i => i.Quantity) })
            .ToDictionaryAsync(r => r.AccountId, r => r.Units, ct);

        // One pass over the cohort; kinds, money, reasons and compensations are folded from these groups
        var cohortGroups = await cohort
            .GroupBy(r => new
            {
                AccountId = r.Order!.MarketplaceOrder!.MarketplaceAccountId,
                r.Kind,
                r.CurrencyCode,
                r.Reason,
                r.CompensationStatus,
            })
            .Select(g => new
            {
                g.Key.AccountId,
                g.Key.Kind,
                g.Key.CurrencyCode,
                g.Key.Reason,
                g.Key.CompensationStatus,
                Units = g.Sum(r => r.Quantity),
                Rows = g.Count(),
                Amount = g.Sum(r => r.Price * r.Quantity),
            })
            .ToListAsync(ct);

        var byKind = cohortGroups
            .GroupBy(r => new { r.AccountId, r.Kind })
            .Select(g => new { g.Key.AccountId, g.Key.Kind, Units = g.Sum(r => r.Units) })
            .ToList();

        // What the marketplace withheld from the payout is only in its accruals, which are not imported
        var money = request.MoneyMode == AnalyticsMoneyMode.Price
            ? cohortGroups
                .Where(r => r.Amount != null && r.CurrencyCode != null)
                .GroupBy(r => new { r.AccountId, CurrencyCode = r.CurrencyCode! })
                .Select(g => new { g.Key.AccountId, g.Key.CurrencyCode, Amount = g.Sum(r => r.Amount!.Value) })
                .ToList()
            : [];

        var topReasons = cohortGroups
            .Where(r => !string.IsNullOrEmpty(r.Reason))
            .GroupBy(r => r.Reason!)
            .Select(g => new ReturnReasonUnitsDto { Reason = g.Key, Units = g.Sum(r => r.Units) })
            .OrderByDescending(r => r.Units)
            .ThenBy(r => r.Reason, StringComparer.Ordinal)
            .Take(TopReasons)
            .ToList();

        var compensations = cohortGroups
            .Where(r => r.CompensationStatus != null)
            .GroupBy(r => r.CompensationStatus!.Value)
            .Select(g => new CompensationCountDto { Status = g.Key, Count = g.Sum(r => r.Rows) })
            .OrderBy(c => c.Status)
            .ToList();

        var days = await queries
            .ReturnsByReturnDate(accountIds, period.FromUtc, period.ToUtc)
            .GroupBy(r => new
            {
                AccountId = r.Order!.MarketplaceOrder!.MarketplaceAccountId,
                Day = r.ReturnedAt!.Value.AddMinutes(offset).Date,
            })
            .Select(g => new DayValue { AccountId = g.Key.AccountId, Day = g.Key.Day, Value = g.Sum(r => r.Quantity) })
            .ToListAsync(ct);

        return new ChannelsReturnsDto
        {
            From = result.From,
            To = result.To,
            TimeZoneId = result.TimeZoneId,
            MoneyMode = result.MoneyMode,
            ReturnsMaturityDays = result.ReturnsMaturityDays,
            ReturnsImmature = result.ReturnsImmature,
            Step = result.Step,
            Intervals = result.Intervals,
            Accounts = accounts
                .Select(a =>
                {
                    var sold = soldUnits.GetValueOrDefault(a.Id);
                    var kinds = byKind.Where(k => k.AccountId == a.Id).ToList();
                    var returned = kinds.Sum(k => k.Units);
                    return new AccountReturnsDto
                    {
                        MarketplaceAccountId = a.Id,
                        MarketplaceType = a.Type,
                        Name = a.Name,
                        SoldUnits = sold,
                        ReturnedUnits = returned,
                        ReturnRate = AnalyticsCalculator.Ratio(returned, sold),
                        ByKind = kinds
                            .OrderBy(k => k.Kind)
                            .Select(k => new ReturnKindUnitsDto { Kind = k.Kind, Units = k.Units })
                            .ToList(),
                        Money = money
                            .Where(m => m.AccountId == a.Id)
                            .OrderBy(m => m.CurrencyCode)
                            .Select(m => new MoneyAmountDto { CurrencyCode = m.CurrencyCode, Amount = m.Amount })
                            .ToList(),
                    };
                })
                .ToList(),
            Series = accounts
                .Select(a => Series(AnalyticsChannelKind.Marketplace, a, intervals, period.Today,
                    days.Where(d => d.AccountId == a.Id)))
                .ToList(),
            TopReasons = topReasons,
            Compensations = compensations,
        };
    }

    public async Task<ChannelsCancellationsDto> GetCancellationsAsync(
        ClaimsPrincipal user, ChannelsCancellationsRequest request, CancellationToken ct = default)
    {
        var period = await queries.ResolvePeriodAsync(request, ct);
        var step = request.Step ?? AnalyticsCalculator.DefaultStep(period.From, period.To);
        var intervals = AnalyticsCalculator.SplitIntervals(period.From, period.To, step);
        var accounts = await queries.LoadAccountsAsync(request, ct);
        var accountIds = accounts.Select(a => a.Id).ToList();
        var offset = period.OffsetMinutes;

        List<OrderOutcomeDay> days = accountIds.Count == 0
            ? []
            : await queries
                .MarketplaceOrders(accountIds, AnalyticsQueries.MarketplaceCountedStatuses, period.FromUtc,
                    period.ToUtc)
                .GroupBy(o => new
                {
                    AccountId = o.MarketplaceOrder!.MarketplaceAccountId,
                    Day = o.EffectiveDate.AddMinutes(offset).Date,
                    IsCancelled = o.MarketplaceOrder.Status == MarketplaceOrderStatus.Cancelled,
                })
                .Select(g => new OrderOutcomeDay
                {
                    AccountId = g.Key.AccountId, Day = g.Key.Day, IsCancelled = g.Key.IsCancelled, Value = g.Count(),
                })
                .ToListAsync(ct);

        return new ChannelsCancellationsDto
        {
            From = period.From,
            To = period.To,
            TimeZoneId = period.TimeZoneId,
            Step = step,
            Intervals = AnalyticsCalculator.ToIntervalDtos(intervals, period.Today),
            Series = accounts
                .Select(a => Series(AnalyticsChannelKind.Marketplace, a, intervals, period.Today,
                    days.Where(d => d.AccountId == a.Id && d.IsCancelled)))
                .ToList(),
            Orders = accounts
                .Select(a => Series(AnalyticsChannelKind.Marketplace, a, intervals, period.Today,
                    days.Where(d => d.AccountId == a.Id)))
                .ToList(),
        };
    }

    public async Task<ChannelsLossesDto> GetLossesAsync(
        ClaimsPrincipal user, ChannelsLossesRequest request, CancellationToken ct = default)
    {
        var period = await queries.ResolvePeriodAsync(request, ct);
        var options = await settings.GetOptionsAsync(ct);
        var step = request.Step ?? AnalyticsCalculator.DefaultStep(period.From, period.To);
        var intervals = AnalyticsCalculator.SplitIntervals(period.From, period.To, step);
        var accounts = await queries.LoadAccountsAsync(request, ct);
        var accountIds = accounts.Select(a => a.Id).ToList();
        var offset = period.OffsetMinutes;

        // Every channel of the selection, marketplace and Direct alike; the date is always the order's
        var outcomes = new List<OrderOutcomeDay>();
        var shopUnits = new List<DayValue>();
        var directUnits = new List<DayValue>();
        var returned = new List<DayValue>();

        if (accountIds.Count > 0)
        {
            outcomes.AddRange(await queries
                .MarketplaceOrders(accountIds, AnalyticsQueries.MarketplaceCountedStatuses, period.FromUtc,
                    period.ToUtc)
                .GroupBy(o => new
                {
                    Day = o.EffectiveDate.AddMinutes(offset).Date,
                    IsCancelled = o.MarketplaceOrder!.Status == MarketplaceOrderStatus.Cancelled,
                })
                .Select(g => new OrderOutcomeDay { Day = g.Key.Day, IsCancelled = g.Key.IsCancelled, Value = g.Count() })
                .ToListAsync(ct));

            shopUnits = await queries
                .SaleLines(accountIds, period.FromUtc, period.ToUtc)
                .GroupBy(i => i.Order.EffectiveDate.AddMinutes(offset).Date)
                .Select(g => new DayValue { Day = g.Key, Value = g.Sum(i => i.Quantity) })
                .ToListAsync(ct);

            returned = await queries
                .CohortReturns(accountIds, period.FromUtc, period.ToUtc)
                .GroupBy(r => r.Order!.EffectiveDate.AddMinutes(offset).Date)
                .Select(g => new DayValue { Day = g.Key, Value = g.Sum(r => r.Quantity) })
                .ToListAsync(ct);
        }

        if (request.IncludeDirect)
        {
            outcomes.AddRange(await queries
                .DirectOrders(request.DirectTagIds, AnalyticsQueries.DirectCountedStatuses, period.FromUtc,
                    period.ToUtc)
                .GroupBy(o => new
                {
                    Day = o.EffectiveDate.AddMinutes(offset).Date,
                    IsCancelled = o.Status == OrderStatus.Canceled,
                })
                .Select(g => new OrderOutcomeDay { Day = g.Key.Day, IsCancelled = g.Key.IsCancelled, Value = g.Count() })
                .ToListAsync(ct));

            if (request.Measure == AnalyticsMeasure.Units)
                directUnits = await queries
                    .DirectOrders(request.DirectTagIds, AnalyticsQueries.DirectSaleStatuses, period.FromUtc,
                        period.ToUtc)
                    .SelectMany(o => o.Boxes)
                    .SelectMany(b => b.Components)
                    .GroupBy(c => c.OrderBox.Order.EffectiveDate.AddMinutes(offset).Date)
                    .Select(g => new DayValue { Day = g.Key, Value = g.Sum(c => c.Quantity) })
                    .ToListAsync(ct);
        }

        List<int?> ByInterval(IEnumerable<DayValue> values) => AnalyticsCalculator.SumByInterval(
            intervals, period.Today, values.Select(d => (DateOnly.FromDateTime(d.Day), d.Value)));

        var saleOrders = ByInterval(outcomes.Where(o => !o.IsCancelled));
        var cancellations = ByInterval(outcomes.Where(o => o.IsCancelled));
        var shopUnitValues = ByInterval(shopUnits);
        var returnedValues = ByInterval(returned);
        var sales = request.Measure == AnalyticsMeasure.Orders
            ? saleOrders
            : ByInterval(shopUnits.Concat(directUnits));

        return new ChannelsLossesDto
        {
            From = period.From,
            To = period.To,
            TimeZoneId = period.TimeZoneId,
            Step = step,
            Measure = request.Measure,
            ReturnsMaturityDays = options.ReturnsMaturityDays,
            Points = AnalyticsCalculator.ToIntervalDtos(intervals, period.Today)
                .Select((interval, i) => new LossesPointDto
                {
                    Interval = interval,
                    ReturnsImmature = accountIds.Count > 0 && !interval.IsFuture
                        && AnalyticsCalculator.IsReturnsImmature(interval.End, period.Today,
                            options.ReturnsMaturityDays),
                    Sales = sales[i],
                    SaleOrders = saleOrders[i],
                    Cancellations = cancellations[i],
                    ShopUnits = accountIds.Count > 0 ? shopUnitValues[i] : null,
                    ReturnedUnits = accountIds.Count > 0 ? returnedValues[i] : null,
                })
                .ToList(),
        };
    }

    public async Task<ChannelsTopItemsDto> GetTopItemsAsync(
        ClaimsPrincipal user, ChannelsTopItemsRequest request, CancellationToken ct = default)
    {
        var period = await queries.ResolvePeriodAsync(request, ct);
        var accounts = await queries.LoadAccountsAsync(request, ct);
        var accountIds = accounts.Select(a => a.Id).ToList();
        var payoutMode = request.MoneyMode == AnalyticsMoneyMode.Payout;

        // Grouped with the null item and currency as well: those lines still count units and unlinked lines
        var lines = accountIds.Count == 0
            ? []
            : await queries
                .SaleLines(accountIds, period.FromUtc, period.ToUtc)
                .GroupBy(i => new { i.CatalogItemId, i.CurrencyCode })
                .Select(g => new
                {
                    g.Key.CatalogItemId,
                    g.Key.CurrencyCode,
                    Units = g.Sum(i => i.Quantity),
                    Lines = g.Count(),
                    Price = g.Sum(i => i.Price != null ? i.Price.Value * i.Quantity : 0),
                })
                .ToListAsync(ct);

        var payouts = accountIds.Count == 0 || !payoutMode
            ? []
            : await queries
                .SaleAccruals(accountIds, period.FromUtc, period.ToUtc)
                .Where(a => a.CatalogItemId != null)
                .GroupBy(a => new { CatalogItemId = a.CatalogItemId!.Value, a.CurrencyCode })
                .Select(g => new { g.Key.CatalogItemId, g.Key.CurrencyCode, Amount = g.Sum(a => a.Amount) })
                .ToListAsync(ct);

        var directUnits = request.IncludeDirect
            ? await queries
                .DirectOrders(request.DirectTagIds, AnalyticsQueries.DirectSaleStatuses, period.FromUtc,
                    period.ToUtc)
                .SelectMany(o => o.Boxes)
                .SelectMany(b => b.Components)
                .GroupBy(c => c.CatalogItemId)
                .Select(g => new { CatalogItemId = g.Key, Units = g.Sum(c => c.Quantity) })
                .ToListAsync(ct)
            : [];

        var currencies = lines
            .Where(l => l.CatalogItemId != null && l.CurrencyCode != null)
            .GroupBy(l => l.CurrencyCode!)
            .OrderByDescending(g => g.Sum(l => l.Lines))
            .ThenBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => g.Key)
            .ToList();
        var currency = request.CurrencyCode != null && currencies.Contains(request.CurrencyCode)
            ? request.CurrencyCode
            : currencies.FirstOrDefault();

        var linked = lines.Where(l => l.CatalogItemId != null).ToList();
        var units = linked
            .Select(l => (Id: l.CatalogItemId!.Value, l.Units))
            .Concat(directUnits.Select(d => (Id: d.CatalogItemId, d.Units)))
            .GroupBy(u => u.Id)
            .ToDictionary(g => g.Key, g => g.Sum(u => u.Units));
        // Amounts with no currency cannot be ranked against anything, as in the summary
        var money = currency == null ? new Dictionary<Guid, decimal>()
            : payoutMode ? payouts
                .Where(p => p.CurrencyCode == currency)
                .ToDictionary(p => p.CatalogItemId, p => p.Amount)
            : linked
                .Where(l => l.CurrencyCode == currency)
                .GroupBy(l => l.CatalogItemId!.Value)
                .ToDictionary(g => g.Key, g => g.Sum(l => l.Price));

        var ranked = (request.By == AnalyticsTopItemsBy.Money
                ? money.Select(m => (Id: m.Key, m.Value))
                : units.Select(u => (Id: u.Key, Value: (decimal)u.Value)))
            .Where(v => v.Value > 0)
            .OrderByDescending(v => v.Value)
            .ThenBy(v => v.Id)
            .ToList();
        var total = ranked.Sum(v => v.Value);
        var shown = request.Take is { } take ? ranked.Take(take).ToList() : ranked;

        var shownIds = shown.Select(v => v.Id).ToList();
        var items = await db.CatalogItems
            .Where(c => shownIds.Contains(c.Id))
            .Select(c => new { c.Id, c.FullName, c.Type })
            .ToDictionaryAsync(c => c.Id, ct);

        return new ChannelsTopItemsDto
        {
            From = period.From,
            To = period.To,
            TimeZoneId = period.TimeZoneId,
            By = request.By,
            MoneyMode = request.MoneyMode,
            CurrencyCode = currency,
            Currencies = currencies,
            TotalItems = ranked.Count,
            UnlinkedLines = lines.Where(l => l.CatalogItemId == null).Sum(l => l.Lines),
            Items = shown
                .Select(v => new TopItemDto
                {
                    CatalogItemId = v.Id,
                    Name = items[v.Id].FullName,
                    Type = items[v.Id].Type,
                    Units = units.GetValueOrDefault(v.Id),
                    Money = money.TryGetValue(v.Id, out var amount) ? amount : null,
                    Share = (double)(v.Value / total),
                })
                .ToList(),
        };
    }

    public async Task<ChannelsWeekdaysDto> GetWeekdaysAsync(
        ClaimsPrincipal user, ChannelsWeekdaysRequest request, CancellationToken ct = default)
    {
        var period = await queries.ResolvePeriodAsync(request, ct);
        var accounts = await queries.LoadAccountsAsync(request, ct);
        // Direct is loaded per order below: its tag rows need each order's tags
        var days = await LoadSaleDaysAsync(request, accounts, request.Measure, includeDirect: false, period, ct);
        var directOrders = request.IncludeDirect
            ? await LoadDirectOrderDaysAsync(request.DirectTagIds, request.Measure, period, ct)
            : [];

        // Only finished days count: today and the days ahead have not sold out yet and would drag the averages down
        var yesterday = period.Today.AddDays(-1);
        DateOnly? countedTo = period.To <= yesterday ? period.To
            : period.From <= yesterday ? yesterday
            : null;

        List<decimal?> Averages(IEnumerable<DayValue> values)
        {
            if (countedTo is not { } to)
                return Enumerable.Repeat<decimal?>(null, 7).ToList();
            var dayValues = values.Select(d => (DateOnly.FromDateTime(d.Day), (decimal)d.Value));
            return (request.Scale == WeekdayScale.WeekShare
                    ? AnalyticsCalculator.WeekdayShareMedians(period.From, to, dayValues)
                    : AnalyticsCalculator.WeekdayAverages(period.From, to, dayValues))
                .ToList();
        }

        var rows = accounts
            .Select(a => new WeekdayRowDto
            {
                Kind = AnalyticsChannelKind.Marketplace,
                MarketplaceAccountId = a.Id,
                MarketplaceType = a.Type,
                Name = a.Name,
                Values = Averages(days.Where(d => d.AccountId == a.Id)),
            })
            .ToList();

        if (request.IncludeDirect)
        {
            // Same rows as the summary: the whole channel, one per tag met in the period, then the untagged
            var tagIds = directOrders.SelectMany(o => o.TagIds).Distinct().ToList();
            var tags = await db.OrderTags
                .Where(t => tagIds.Contains(t.Id))
                .OrderBy(t => t.Name)
                .Select(t => new { t.Id, t.Name })
                .ToListAsync(ct);

            rows.Add(new WeekdayRowDto { Kind = AnalyticsChannelKind.Direct, Values = Averages(directOrders) });
            rows.AddRange(tags.Select(t => new WeekdayRowDto
            {
                Kind = AnalyticsChannelKind.DirectTag,
                TagId = t.Id,
                Name = t.Name,
                Values = Averages(directOrders.Where(o => o.TagIds.Contains(t.Id))),
            }));
            rows.Add(new WeekdayRowDto
            {
                Kind = AnalyticsChannelKind.DirectUntagged,
                Values = Averages(directOrders.Where(o => o.TagIds.Count == 0)),
            });
        }

        return new ChannelsWeekdaysDto
        {
            From = period.From,
            To = period.To,
            TimeZoneId = period.TimeZoneId,
            Measure = request.Measure,
            Scale = request.Scale,
            CountedTo = countedTo,
            FullWeeks = countedTo is { } counted ? AnalyticsCalculator.FullWeeks(period.From, counted).Count : 0,
            Total = Averages(days.Concat(directOrders)),
            Rows = rows,
        };
    }

    /// <summary>Sale orders or units per channel and day; Direct counts as one channel, tags only narrow it.</summary>
    private async Task<List<DayValue>> LoadSaleDaysAsync(
        AnalyticsFilterRequest request,
        List<AnalyticsAccount> accounts,
        AnalyticsMeasure measure,
        bool includeDirect,
        AnalyticsPeriod period,
        CancellationToken ct)
    {
        var accountIds = accounts.Select(a => a.Id).ToList();
        var offset = period.OffsetMinutes;
        var days = new List<DayValue>();

        if (accountIds.Count > 0)
        {
            days.AddRange(measure == AnalyticsMeasure.Orders
                ? await queries
                    .MarketplaceOrders(accountIds, AnalyticsQueries.MarketplaceSaleStatuses, period.FromUtc,
                        period.ToUtc)
                    .GroupBy(o => new
                    {
                        AccountId = o.MarketplaceOrder!.MarketplaceAccountId,
                        Day = o.EffectiveDate.AddMinutes(offset).Date,
                    })
                    .Select(g => new DayValue { AccountId = g.Key.AccountId, Day = g.Key.Day, Value = g.Count() })
                    .ToListAsync(ct)
                : await queries
                    .SaleLines(accountIds, period.FromUtc, period.ToUtc)
                    .GroupBy(i => new
                    {
                        AccountId = i.Order.MarketplaceOrder!.MarketplaceAccountId,
                        Day = i.Order.EffectiveDate.AddMinutes(offset).Date,
                    })
                    .Select(g => new DayValue
                    {
                        AccountId = g.Key.AccountId, Day = g.Key.Day, Value = g.Sum(i => i.Quantity),
                    })
                    .ToListAsync(ct));
        }

        if (includeDirect)
        {
            var direct = queries.DirectOrders(
                request.DirectTagIds, AnalyticsQueries.DirectSaleStatuses, period.FromUtc, period.ToUtc);

            days.AddRange(measure == AnalyticsMeasure.Orders
                ? await direct
                    .GroupBy(o => o.EffectiveDate.AddMinutes(offset).Date)
                    .Select(g => new DayValue { Day = g.Key, Value = g.Count() })
                    .ToListAsync(ct)
                : await direct
                    .SelectMany(o => o.Boxes)
                    .SelectMany(b => b.Components)
                    .GroupBy(c => c.OrderBox.Order.EffectiveDate.AddMinutes(offset).Date)
                    .Select(g => new DayValue { Day = g.Key, Value = g.Sum(c => c.Quantity) })
                    .ToListAsync(ct));
        }

        return days;
    }

    /// <summary>One entry per Direct sale order with its tags: the value is the order itself or its units.</summary>
    private Task<List<DirectOrderDay>> LoadDirectOrderDaysAsync(
        Guid[]? tagIds, AnalyticsMeasure measure, AnalyticsPeriod period, CancellationToken ct)
    {
        var offset = period.OffsetMinutes;
        var units = measure == AnalyticsMeasure.Units;

        return queries
            .DirectOrders(tagIds, AnalyticsQueries.DirectSaleStatuses, period.FromUtc, period.ToUtc)
            .Select(o => new DirectOrderDay
            {
                Day = o.EffectiveDate.AddMinutes(offset).Date,
                Value = units ? o.Boxes.SelectMany(b => b.Components).Sum(c => c.Quantity) : 1,
                TagIds = o.Tags.Select(t => t.Id).ToList(),
            })
            .ToListAsync(ct);
    }

    private async Task<Dictionary<Guid, ChannelSummaryRowDto>> BuildMarketplaceRowsAsync(
        List<Guid> accountIds, AnalyticsPeriod period, AnalyticsMoneyMode moneyMode, CancellationToken ct)
    {
        var orderCounts = await queries
            .MarketplaceOrders(accountIds, AnalyticsQueries.MarketplaceCountedStatuses, period.PreviousFromUtc,
                period.ToUtc)
            .GroupBy(o => new
            {
                AccountId = o.MarketplaceOrder!.MarketplaceAccountId,
                IsCurrent = o.EffectiveDate >= period.FromUtc,
                IsSale = o.MarketplaceOrder.Status != MarketplaceOrderStatus.Cancelled,
            })
            .Select(g => new { g.Key.AccountId, g.Key.IsCurrent, g.Key.IsSale, Count = g.Count() })
            .ToListAsync(ct);

        var lines = await queries
            .SaleLines(accountIds, period.FromUtc, period.ToUtc)
            // Accrual is in the key rather than in a Count: a navigation read inside an aggregate becomes a
            // subquery per group. It is the order's, so an order's lines never split and distinct counts add up.
            .GroupBy(i => new
            {
                AccountId = i.Order.MarketplaceOrder!.MarketplaceAccountId,
                i.CurrencyCode,
                Accrued = i.Order.IsAccrued,
            })
            .Select(g => new LineAggregate
            {
                AccountId = g.Key.AccountId,
                CurrencyCode = g.Key.CurrencyCode,
                Accrued = g.Key.Accrued,
                Units = g.Sum(i => i.Quantity),
                Lines = g.Count(),
                PriceRevenue = g.Sum(i => i.Price != null ? i.Price.Value * i.Quantity : 0),
                PricedOrders = g.Where(i => i.Price != null).Select(i => i.OrderId).Distinct().Count(),
                DiscountPrice = g.Sum(i => i.Price != null && i.OldPrice > 0 ? i.Price.Value * i.Quantity : 0),
                DiscountOldPrice = g.Sum(i => i.Price != null && i.OldPrice > 0 ? i.OldPrice!.Value * i.Quantity : 0),
                DiscountAmount = g.Sum(i => i.DiscountValue != null ? i.DiscountValue.Value * i.Quantity : 0),
            })
            .ToListAsync(ct);

        var returnedUnits = await queries
            .CohortReturns(accountIds, period.FromUtc, period.ToUtc)
            .GroupBy(r => r.Order!.MarketplaceOrder!.MarketplaceAccountId)
            .Select(g => new { AccountId = g.Key, Quantity = g.Sum(r => r.Quantity) })
            .ToDictionaryAsync(r => r.AccountId, r => r.Quantity, ct);

        var payouts = moneyMode == AnalyticsMoneyMode.Payout
            ? await queries
                .SaleAccruals(accountIds, period.FromUtc, period.ToUtc)
                .GroupBy(a => new { a.MarketplaceAccountId, a.CurrencyCode, a.OrderId })
                .Select(g => new
                {
                    g.Key.MarketplaceAccountId,
                    g.Key.CurrencyCode,
                    Net = g.Sum(a => a.Amount),
                    // A fully reversed sale earned nothing; its negative net would only drag the average check down
                    Kept = g.Sum(a => a.Category == MarketplaceAccrualCategory.Sale ? a.Amount : 0) > 0,
                })
                .GroupBy(o => new { o.MarketplaceAccountId, o.CurrencyCode })
                .Select(g => new PayoutAggregate
                {
                    AccountId = g.Key.MarketplaceAccountId,
                    CurrencyCode = g.Key.CurrencyCode!,
                    Revenue = g.Sum(o => o.Net),
                    KeptRevenue = g.Sum(o => o.Kept ? o.Net : 0),
                    KeptOrders = g.Count(o => o.Kept),
                })
                .ToListAsync(ct)
            : [];

        var result = new Dictionary<Guid, ChannelSummaryRowDto>();
        foreach (var accountId in accountIds)
        {
            int Count(bool isCurrent, bool isSale) => orderCounts
                .Where(c => c.AccountId == accountId && c.IsCurrent == isCurrent && c.IsSale == isSale)
                .Sum(c => c.Count);

            var orders = Count(isCurrent: true, isSale: true);
            var cancellations = Count(isCurrent: true, isSale: false);
            var accountLines = lines.Where(l => l.AccountId == accountId).ToList();
            var units = accountLines.Sum(l => l.Units);
            var returned = returnedUnits.GetValueOrDefault(accountId);
            var moneyLines = accountLines.Where(l => l.CurrencyCode != null).ToLookup(l => l.CurrencyCode!);
            var accountPayouts = payouts.Where(p => p.AccountId == accountId).ToDictionary(p => p.CurrencyCode);

            result[accountId] = new ChannelSummaryRowDto
            {
                Orders = orders,
                PreviousOrders = Count(isCurrent: false, isSale: true),
                Units = units,
                Cancellations = cancellations,
                CancellationRate = AnalyticsCalculator.Ratio(cancellations, orders + cancellations),
                ReturnedUnits = returned,
                ReturnRate = AnalyticsCalculator.Ratio(returned, units),
                PayoutCoverage = AnalyticsCalculator.Ratio(
                    moneyLines.SelectMany(g => g).Where(l => l.Accrued).Sum(l => l.Lines),
                    moneyLines.SelectMany(g => g).Sum(l => l.Lines)),
                Money = moneyLines
                    .Select(g => g.Key)
                    .Union(accountPayouts.Keys)
                    .Order(StringComparer.Ordinal)
                    .Select(currency =>
                    {
                        var currencyLines = moneyLines[currency].ToList();
                        var payout = accountPayouts.GetValueOrDefault(currency);
                        var (revenue, checkRevenue, checkOrders) = moneyMode == AnalyticsMoneyMode.Payout
                            ? (payout?.Revenue ?? 0, payout?.KeptRevenue ?? 0, payout?.KeptOrders ?? 0)
                            : (currencyLines.Sum(l => l.PriceRevenue), currencyLines.Sum(l => l.PriceRevenue),
                                currencyLines.Sum(l => l.PricedOrders));
                        return new ChannelMoneyDto
                        {
                            CurrencyCode = currency,
                            Revenue = revenue,
                            AverageCheck = checkOrders == 0 ? null : checkRevenue / checkOrders,
                            DiscountDepth = AnalyticsCalculator.DiscountDepth(
                                currencyLines.Sum(l => l.DiscountPrice), currencyLines.Sum(l => l.DiscountOldPrice)),
                            DiscountAmount = currencyLines.Sum(l => l.DiscountAmount),
                        };
                    })
                    .ToList(),
            };
        }

        return result;
    }

    private async Task<List<ChannelSummaryRowDto>> BuildDirectRowsAsync(
        Guid[]? tagIds, AnalyticsPeriod period, CancellationToken ct)
    {
        var orders = await queries
            .DirectOrders(tagIds, AnalyticsQueries.DirectCountedStatuses, period.PreviousFromUtc, period.ToUtc)
            .Select(o => new DirectOrderRow
            {
                IsCurrent = o.EffectiveDate >= period.FromUtc,
                IsSale = o.Status != OrderStatus.Canceled,
                Units = o.Boxes.SelectMany(b => b.Components).Sum(c => c.Quantity),
                TagIds = o.Tags.Select(t => t.Id).ToList(),
            })
            .ToListAsync(ct);

        var currentTagIds = orders.Where(o => o.IsCurrent).SelectMany(o => o.TagIds).Distinct().ToList();
        var tags = await db.OrderTags
            .Where(t => currentTagIds.Contains(t.Id))
            .OrderBy(t => t.Name)
            .Select(t => new { t.Id, t.Name })
            .ToListAsync(ct);

        var rows = new List<ChannelSummaryRowDto>
        {
            DirectRow(AnalyticsChannelKind.Direct, null, null, orders),
        };
        rows.AddRange(tags.Select(t => DirectRow(
            AnalyticsChannelKind.DirectTag, t.Id, t.Name, orders.Where(o => o.TagIds.Contains(t.Id)))));
        rows.Add(DirectRow(AnalyticsChannelKind.DirectUntagged, null, null, orders.Where(o => o.TagIds.Count == 0)));

        return rows;
    }

    private static ChannelSummaryRowDto DirectRow(
        AnalyticsChannelKind kind, Guid? tagId, string? name, IEnumerable<DirectOrderRow> orders)
    {
        var list = orders.ToList();
        var sales = list.Count(o => o.IsCurrent && o.IsSale);
        var cancellations = list.Count(o => o.IsCurrent && !o.IsSale);

        return new ChannelSummaryRowDto
        {
            Kind = kind,
            TagId = tagId,
            Name = name,
            Orders = sales,
            PreviousOrders = list.Count(o => !o.IsCurrent && o.IsSale),
            Units = list.Where(o => o.IsCurrent && o.IsSale).Sum(o => o.Units),
            Cancellations = cancellations,
            CancellationRate = AnalyticsCalculator.Ratio(cancellations, sales + cancellations),
        };
    }

    private async Task<(List<ChannelCancellationsDto> ByAccount, List<CancelReasonCountDto> TopReasons)>
        BuildCancellationsAsync(List<Guid> accountIds, AnalyticsPeriod period, CancellationToken ct)
    {
        var cancelled = queries
            .MarketplaceOrders(accountIds, [MarketplaceOrderStatus.Cancelled], period.FromUtc, period.ToUtc)
            .Select(o => o.MarketplaceOrder!);

        var byType = await cancelled
            .GroupBy(m => new
            {
                m.MarketplaceAccountId,
                Type = m.CancellationType ?? MarketplaceCancellationType.Unknown,
            })
            .Select(g => new
            {
                g.Key.MarketplaceAccountId,
                g.Key.Type,
                Count = g.Count(),
                AfterShip = g.Count(m => m.CancelledAfterShip == true),
            })
            .ToListAsync(ct);

        var topReasons = await cancelled
            .Where(m => m.CancelReason != null && m.CancelReason != "")
            .GroupBy(m => m.CancelReason!)
            .Select(g => new CancelReasonCountDto { Reason = g.Key, Count = g.Count() })
            .OrderByDescending(r => r.Count)
            .ThenBy(r => r.Reason)
            .Take(TopReasons)
            .ToListAsync(ct);

        var byAccount = byType
            .GroupBy(r => r.MarketplaceAccountId)
            .Select(g => new ChannelCancellationsDto
            {
                MarketplaceAccountId = g.Key,
                ByType = g.OrderBy(r => r.Type)
                    .Select(r => new CancellationTypeCountDto { Type = r.Type, Count = r.Count })
                    .ToList(),
                AfterShip = g.Sum(r => r.AfterShip),
            })
            .ToList();

        return (byAccount, topReasons);
    }

    private static ChannelSeriesDto Series(
        AnalyticsChannelKind kind,
        AnalyticsAccount? account,
        IReadOnlyList<AnalyticsInterval> intervals,
        DateOnly today,
        IEnumerable<DayValue> days)
    {
        var values = AnalyticsCalculator.SumByInterval(
            intervals, today, days.Select(d => (DateOnly.FromDateTime(d.Day), d.Value)));

        return new ChannelSeriesDto
        {
            Kind = kind,
            MarketplaceAccountId = account?.Id,
            MarketplaceType = account?.Type,
            Name = account?.Name,
            Values = values,
            Total = values.Sum(v => v ?? 0),
        };
    }
}
