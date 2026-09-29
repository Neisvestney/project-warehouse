using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using ProjectWarehouse.Server.Data;
using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Infrastructure;
using ProjectWarehouse.Server.Integrations.Abstractions;
using ProjectWarehouse.Server.Models;
using ProjectWarehouse.Server.Models.Analytics;

namespace ProjectWarehouse.Server.Services;

public class AnalyticsPayoutsService(
    ApplicationDbContext db,
    AnalyticsQueries queries,
    IAnalyticsSettingsService settings,
    IMarketplaceProviderRegistry providers) : IAnalyticsPayoutsService
{
    /// <summary>Journal days are the marketplace's accounting days, which for Ozon are Moscow days.</summary>
    private static readonly TimeZoneInfo JournalZone =
        TimeZoneInfo.TryFindSystemTimeZoneById("Europe/Moscow", out var zone)
            ? zone
            : TimeZoneInfo.CreateCustomTimeZone("MSK", TimeSpan.FromHours(3), "MSK", "MSK");

    /// <summary>A line of a posting the journal holds no sale for, in one currency.</summary>
    private sealed class OpenLine
    {
        public Guid OrderId { get; init; }
        public Guid AccountId { get; init; }
        public MarketplaceOrderStatus Status { get; init; }
        public DateTime EffectiveDate { get; init; }
        public string CurrencyCode { get; init; } = null!;
        public decimal Amount { get; init; }
    }

    private sealed class AccountCurrencySum
    {
        public Guid AccountId { get; init; }
        public string CurrencyCode { get; init; } = null!;
        public decimal Amount { get; init; }
    }

    private sealed class CategorySum
    {
        public Guid AccountId { get; init; }
        public string CurrencyCode { get; init; } = null!;
        public MarketplaceAccrualCategory Category { get; init; }
        public MarketplaceAccrualSource Source { get; init; }
        public bool ByPosting { get; init; }
        public decimal Amount { get; init; }
    }

    private enum Bucket { InTransit, DeliveredNotAccrued, NotAccruedByMarketplace }

    private sealed record OpenPosting(Bucket Bucket, int AgeDays, decimal Amount);

    public async Task<PayoutsDto> GetPayoutsAsync(
        ClaimsPrincipal user, PayoutsRequest request, CancellationToken ct = default)
    {
        var (bounds, clock) = await ResolvePeriodAsync(request, ct);
        var options = await settings.GetOptionsAsync(ct);
        var accounts = await LoadAccountsAsync(request, ct);
        var accountIds = accounts.Select(a => a.Id).ToList();

        var coverage = await LoadCoverageAsync(accountIds, ct);
        var buyoutsLoadedFrom = accountIds.Count == 0
            ? []
            : await db.MarketplaceAccounts
                .Where(a => accountIds.Contains(a.Id))
                .ToDictionaryAsync(a => a.Id, a => a.BuyoutsLoadedFrom, ct);
        var coverageStart = ToCoverageStart(coverage);

        var openLines = (accountIds.Count == 0 ? [] : await LoadOpenLinesAsync(accountIds, coverageStart, ct))
            .ToLookup(l => l.AccountId);
        var uncoveredCounts = await CountUncoveredAsync(accountIds, coverageStart, ct);
        var ratios = accountIds.Count == 0 ? [] : await LoadRatiosAsync(accountIds, clock.Today, options, ct);
        var accruedPostings = accountIds.Count == 0
            ? []
            : await CountAccruedPostingsAsync(accountIds, bounds?.From, bounds?.To, ct);
        var categories = (accountIds.Count == 0
                ? []
                : await LoadCategoriesAsync(accountIds, bounds?.From, bounds?.To, ct))
            .ToLookup(c => (c.AccountId, c.CurrencyCode));

        var offset = TimeSpan.FromMinutes(clock.OffsetMinutes);
        var rows = new List<PayoutsRowDto>();

        foreach (var account in accounts)
        {
            var coveredFrom = coverage.TryGetValue(account.Id, out var c) ? c : (DateOnly?)null;
            var open = new Dictionary<string, List<OpenPosting>>();

            foreach (var posting in openLines[account.Id].GroupBy(l => (l.OrderId, l.CurrencyCode)))
            {
                var (bucket, age) = Classify(posting.First(), coverageStart, clock.Today, offset, options);
                if (bucket is not { } b)
                    continue;

                if (!open.TryGetValue(posting.Key.CurrencyCode, out var list))
                    open[posting.Key.CurrencyCode] = list = [];
                list.Add(new OpenPosting(b, age, posting.Sum(l => l.Amount)));
            }

            var accountRatios = ratios.GetValueOrDefault(account.Id) ?? [];
            var money = open.Keys
                .Union(accountRatios.Keys)
                .Union(categories.Where(g => g.Key.AccountId == account.Id).Select(g => g.Key.CurrencyCode))
                .Order(StringComparer.Ordinal)
                .Select(currency => BuildMoney(
                    currency,
                    open.GetValueOrDefault(currency) ?? [],
                    accountRatios.TryGetValue(currency, out var r) ? r : null,
                    accruedPostings.GetValueOrDefault((account.Id, currency)),
                    categories[(account.Id, currency)]
                        .Select(x => new PayoutsCategoryDto
                        {
                            Category = x.Category,
                            Source = x.Source,
                            ByPosting = x.ByPosting,
                            Amount = x.Amount,
                        })
                        .ToList(),
                    options))
                .ToList();

            rows.Add(new PayoutsRowDto
            {
                MarketplaceAccountId = account.Id,
                MarketplaceType = account.Type,
                Name = account.Name,
                CoveredFrom = coveredFrom,
                BuyoutsLoadedFrom = buyoutsLoadedFrom.GetValueOrDefault(account.Id),
                BuyoutsPending = coveredFrom is { } journalFrom
                    && providers.TryGet(account.Type, out var provider)
                    && provider.Capabilities.HasFlag(MarketplaceCapabilities.Buyouts)
                    && (buyoutsLoadedFrom.GetValueOrDefault(account.Id) is not { } loaded || loaded > journalFrom),
                UncoveredPostings = uncoveredCounts.GetValueOrDefault(account.Id),
                Money = money,
            });
        }

        return new PayoutsDto
        {
            From = bounds?.From ?? (coverage.Count == 0 ? clock.Today : coverage.Values.Min()),
            To = bounds?.To ?? clock.Today,
            Today = clock.Today,
            TimeZoneId = clock.TimeZoneId,
            Settings = new PayoutsAppliedSettingsDto
            {
                PayoutRatioWindowDays = options.PayoutRatioWindowDays,
                PayoutAgeBoundaries = options.PayoutAgeBoundaries,
                PayoutOverdueDays = options.PayoutOverdueDays,
                PayoutNotAccruedDays = options.PayoutNotAccruedDays,
            },
            Rows = rows,
            Totals = BuildTotals(rows, options.PayoutAgeBoundaries.Count + 1),
        };
    }

    public async Task<PayoutsTimeseriesDto> GetTimeseriesAsync(
        ClaimsPrincipal user, PayoutsTimeseriesRequest request, CancellationToken ct = default)
    {
        var (bounds, clock) = await ResolvePeriodAsync(request, ct);
        var accounts = await LoadAccountsAsync(request, ct);
        var accountIds = accounts.Select(a => a.Id).ToList();
        var periodFrom = bounds?.From;
        var periodTo = bounds?.To;

        var days = accountIds.Count == 0
            ? []
            : await db.MarketplaceAccruals
                .Where(a => accountIds.Contains(a.MarketplaceAccountId)
                    && a.CurrencyCode != null
                    && (periodFrom == null || a.Date >= periodFrom)
                    && (periodTo == null || a.Date <= periodTo))
                .GroupBy(a => new { a.MarketplaceAccountId, a.CurrencyCode, a.Date })
                .Select(g => new
                {
                    AccountId = g.Key.MarketplaceAccountId,
                    CurrencyCode = g.Key.CurrencyCode!,
                    g.Key.Date,
                    Amount = g.Sum(a => a.Amount),
                })
                .ToListAsync(ct);

        // Over all time a Moscow journal day can run ahead of the caller's today; it is not a future interval, or
        // its lines would drop out and the intervals would no longer add up to «Начислено»
        var today = bounds is not null || days.Count == 0
            ? clock.Today
            : DateOnly.FromDayNumber(Math.Max(days.Max(d => d.Date).DayNumber, clock.Today.DayNumber));
        var to = bounds?.To ?? today;
        var from = bounds?.From ?? (days.Count == 0 ? to : days.Min(d => d.Date));
        var step = request.Step ?? AnalyticsCalculator.DefaultStep(from, to);

        if (step == AnalyticsStep.Day && to.DayNumber - from.DayNumber + 1 > AnalyticsCalculator.MaxPeriodDays)
            throw new ValidationException("step", ErrorCode.OutOfRange,
                $"A day step allows at most {AnalyticsCalculator.MaxPeriodDays} days.");

        var intervals = AnalyticsCalculator.SplitIntervals(from, to, step);
        var byKey = days.ToLookup(d => (d.AccountId, d.CurrencyCode), d => (d.Date, d.Amount));

        return new PayoutsTimeseriesDto
        {
            From = from,
            To = to,
            TimeZoneId = clock.TimeZoneId,
            Step = step,
            Intervals = AnalyticsCalculator.ToIntervalDtos(intervals, today),
            Currencies = days
                .Select(d => d.CurrencyCode)
                .Distinct()
                .Order(StringComparer.Ordinal)
                .Select(currency => new PayoutsCurrencySeriesDto
                {
                    CurrencyCode = currency,
                    Series = accounts
                        .Select(account =>
                        {
                            var values = AnalyticsCalculator.SumByInterval(
                                intervals, today, byKey[(account.Id, currency)]);
                            return new PayoutsSeriesDto
                            {
                                MarketplaceAccountId = account.Id,
                                MarketplaceType = account.Type,
                                Name = account.Name,
                                Values = values,
                                Total = values.Sum(v => v ?? 0),
                            };
                        })
                        .ToList(),
                })
                .ToList(),
        };
    }

    public async Task<Paginated<PayoutsPostingDto>> GetPostingsAsync(
        ClaimsPrincipal user, PayoutsPostingsRequest request, CancellationToken ct = default)
    {
        var clock = await queries.ResolveClockAsync(ct);
        var options = await settings.GetOptionsAsync(ct);
        var accounts = await queries.LoadAccountsAsync(new AnalyticsFilterRequest
        {
            IncludeMarketplaces = request.IncludeMarketplaces,
            MarketplaceAccountIds = request.MarketplaceAccountIds,
        }, ct);
        var accountIds = accounts.Select(a => a.Id).ToList();
        if (accountIds.Count == 0)
            return new Paginated<PayoutsPostingDto> { Page = request.Page, PageSize = request.PageSize };

        var coverageStart = ToCoverageStart(await LoadCoverageAsync(accountIds, ct));
        var offset = TimeSpan.FromMinutes(clock.OffsetMinutes);

        // Classified in memory, as on the page itself, so the list always matches the bucket's count
        var postings = (await LoadOpenLinesAsync(accountIds, coverageStart, ct))
            .Where(l => l.CurrencyCode == request.CurrencyCode)
            .GroupBy(l => l.OrderId)
            .Select(g =>
            {
                var first = g.First();
                var (bucket, age) = Classify(first, coverageStart, clock.Today, offset, options);
                return (First: first, Bucket: bucket, Age: age, Amount: g.Sum(l => l.Amount));
            })
            .Where(p => request.Bucket switch
            {
                PayoutsBucket.InTransit => p.Bucket == Bucket.InTransit,
                PayoutsBucket.InTransitOverdue => p.Bucket == Bucket.InTransit && p.Age >= options.PayoutOverdueDays,
                PayoutsBucket.DeliveredNotAccrued => p.Bucket == Bucket.DeliveredNotAccrued,
                PayoutsBucket.NotAccruedByMarketplace => p.Bucket == Bucket.NotAccruedByMarketplace,
                _ => false,
            })
            .OrderByDescending(p => p.Age)
            .ThenBy(p => p.First.OrderId)
            .ToList();

        var page = postings.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToList();
        var pageIds = page.Select(p => p.First.OrderId).ToList();
        var orders = await db.Orders
            .Where(o => pageIds.Contains(o.Id))
            .Select(o => new
            {
                o.Id,
                o.Number,
                o.MarketplaceOrder!.PostingNumber,
                o.MarketplaceOrder.DeliveredAt,
            })
            .ToDictionaryAsync(o => o.Id, ct);
        var accountsById = accounts.ToDictionary(a => a.Id);

        return new Paginated<PayoutsPostingDto>
        {
            Items = page
                .Select(p =>
                {
                    var order = orders[p.First.OrderId];
                    var account = accountsById[p.First.AccountId];
                    return new PayoutsPostingDto
                    {
                        OrderId = order.Id,
                        OrderNumber = order.Number,
                        PostingNumber = order.PostingNumber,
                        MarketplaceAccountId = account.Id,
                        MarketplaceType = account.Type,
                        AccountName = account.Name,
                        Status = p.First.Status,
                        EffectiveDate = p.First.EffectiveDate,
                        DeliveredAt = order.DeliveredAt,
                        AgeDays = p.Age,
                        Amount = p.Amount,
                    };
                })
                .ToList(),
            Total = postings.Count,
            Page = request.Page,
            PageSize = request.PageSize,
        };
    }

    /// <summary>
    /// The bucket of a posting with no sale, or null for a delivered one dated before its shop's journal: that sale
    /// was accrued before the journal started, and <see cref="CountUncoveredAsync"/> counts it instead.
    /// </summary>
    private static (Bucket? Bucket, int AgeDays) Classify(
        OpenLine line, Dictionary<Guid, DateTime> coverageStart, DateOnly today, TimeSpan offset,
        AnalyticsOptions options)
    {
        var age = today.DayNumber - DateOnly.FromDateTime(line.EffectiveDate + offset).DayNumber;

        if (line.Status == MarketplaceOrderStatus.Delivering)
            return (Bucket.InTransit, age);
        if (line.EffectiveDate < coverageStart[line.AccountId])
            return (null, age);
        return (age >= options.PayoutNotAccruedDays ? Bucket.NotAccruedByMarketplace : Bucket.DeliveredNotAccrued,
            age);
    }

    /// <summary>First journal day per shop that has one. Buyout rows are dated by delivery, which may precede it.</summary>
    private async Task<Dictionary<Guid, DateOnly>> LoadCoverageAsync(List<Guid> accountIds, CancellationToken ct) =>
        accountIds.Count == 0
            ? []
            : await db.MarketplaceAccruals
                .Where(a => accountIds.Contains(a.MarketplaceAccountId)
                    && a.Source == MarketplaceAccrualSource.AccrualJournal)
                .GroupBy(a => a.MarketplaceAccountId)
                .Select(g => new { g.Key, From = g.Min(a => a.Date) })
                .ToDictionaryAsync(x => x.Key, x => x.From, ct);

    private static Dictionary<Guid, DateTime> ToCoverageStart(Dictionary<Guid, DateOnly> coverage) =>
        coverage.ToDictionary(
            c => c.Key,
            c => TimeZoneInfo.ConvertTimeToUtc(c.Value.ToDateTime(TimeOnly.MinValue), JournalZone));

    /// <summary>The bounded period, or null for all time, and the caller's clock either way.</summary>
    private async Task<(AnalyticsPeriod? Bounds, AnalyticsClock Clock)> ResolvePeriodAsync(
        PayoutsRequest request, CancellationToken ct)
    {
        if (request.From.HasValue != request.To.HasValue)
            throw new ValidationException(request.From.HasValue ? "to" : "from", ErrorCode.InvalidValue,
                "The period must have both ends or neither.");

        if (request is not { From: { } from, To: { } to })
            return (null, await queries.ResolveClockAsync(ct));

        var bounds = await queries.ResolvePeriodAsync(new AnalyticsFilterRequest { From = from, To = to }, ct);
        return (bounds, new AnalyticsClock(bounds.Today, bounds.OffsetMinutes, bounds.TimeZoneId));
    }

    private Task<List<AnalyticsAccount>> LoadAccountsAsync(PayoutsRequest request, CancellationToken ct) =>
        queries.LoadAccountsAsync(new AnalyticsFilterRequest
        {
            IncludeMarketplaces = request.IncludeMarketplaces,
            MarketplaceAccountIds = request.MarketplaceAccountIds,
        }, ct);

    /// <summary>
    /// Lines of the postings with no sale in the journal: every one in transit, and the delivered ones from the
    /// earliest journal start on — the debt buckets are not cut by the period. Delivered lines before their own
    /// shop's start still come back when another shop's journal starts earlier; the caller drops them.
    /// </summary>
    private Task<List<OpenLine>> LoadOpenLinesAsync(
        List<Guid> accountIds, Dictionary<Guid, DateTime> coverageStart, CancellationToken ct)
    {
        var coveredIds = coverageStart.Keys.ToList();
        var earliest = coverageStart.Count == 0 ? DateTime.UnixEpoch : coverageStart.Values.Min();

        return db.OrderMarketplaceItems
            .Where(i => i.Order.MarketplaceOrder != null
                && accountIds.Contains(i.Order.MarketplaceOrder.MarketplaceAccountId)
                && (i.Order.MarketplaceOrder.Status == MarketplaceOrderStatus.Delivering
                    || (i.Order.MarketplaceOrder.Status == MarketplaceOrderStatus.Delivered
                        && coveredIds.Contains(i.Order.MarketplaceOrder.MarketplaceAccountId)
                        && i.Order.EffectiveDate >= earliest))
                && !i.Order.MarketplaceAccruals.Any(a => a.Category == MarketplaceAccrualCategory.Sale)
                && i.Price != null
                && i.CurrencyCode != null)
            .Select(i => new OpenLine
            {
                OrderId = i.OrderId,
                AccountId = i.Order.MarketplaceOrder!.MarketplaceAccountId,
                Status = i.Order.MarketplaceOrder.Status,
                EffectiveDate = i.Order.EffectiveDate,
                CurrencyCode = i.CurrencyCode!,
                Amount = i.Price!.Value * i.Quantity,
            })
            .ToListAsync(ct);
    }

    /// <summary>Delivered postings with no sale dated before the shop's journal, or all of them without one.</summary>
    private async Task<Dictionary<Guid, int>> CountUncoveredAsync(
        List<Guid> accountIds, Dictionary<Guid, DateTime> coverageStart, CancellationToken ct)
    {
        var counts = new Dictionary<Guid, int>();
        foreach (var accountId in accountIds)
        {
            DateTime? start = coverageStart.TryGetValue(accountId, out var s) ? s : null;
            counts[accountId] = await db.Orders.CountAsync(o => o.MarketplaceOrder != null
                && o.MarketplaceOrder.MarketplaceAccountId == accountId
                && o.MarketplaceOrder.Status == MarketplaceOrderStatus.Delivered
                && (start == null || o.EffectiveDate < start)
                && !o.MarketplaceAccruals.Any(a => a.Category == MarketplaceAccrualCategory.Sale), ct);
        }

        return counts;
    }

    /// <summary>
    /// Journal net over sale price, per shop and currency, of the postings whose sale was accrued within the
    /// window. Reversed sales and return logistics arrive days after the sale, so a fresh window would miss them
    /// while an older one would not; both are left out and the ratio reads as what a kept sale brings. Postings the
    /// marketplace bought out are left out too: their price is far below a sale's and would drag the estimate of
    /// ordinary sales down. A net at or below zero gives no ratio: it would turn the debt into a negative estimate.
    /// </summary>
    private async Task<Dictionary<Guid, Dictionary<string, decimal>>> LoadRatiosAsync(
        List<Guid> accountIds, DateOnly today, AnalyticsOptions options, CancellationToken ct)
    {
        var windowFrom = today.AddDays(1 - options.PayoutRatioWindowDays);

        var orderIds = db.Orders
            .Where(o => o.MarketplaceOrder != null
                && accountIds.Contains(o.MarketplaceOrder.MarketplaceAccountId)
                && o.MarketplaceAccruals.Any(a => a.Category == MarketplaceAccrualCategory.Sale
                    && a.Date >= windowFrom && a.Date <= today)
                && !o.MarketplaceAccruals.Any(a => a.Category == MarketplaceAccrualCategory.Sale && a.Amount < 0)
                && !o.MarketplaceAccruals.Any(a => a.Source == MarketplaceAccrualSource.BuyoutReport))
            .Select(o => o.Id);

        var net = await db.MarketplaceAccruals
            .Where(a => a.OrderId != null
                && orderIds.Contains(a.OrderId.Value)
                && a.Category != MarketplaceAccrualCategory.ReturnLogistics
                && a.CurrencyCode != null)
            .GroupBy(a => new { a.MarketplaceAccountId, a.CurrencyCode })
            .Select(g => new AccountCurrencySum
            {
                AccountId = g.Key.MarketplaceAccountId,
                CurrencyCode = g.Key.CurrencyCode!,
                Amount = g.Sum(a => a.Amount),
            })
            .ToListAsync(ct);

        var price = await db.OrderMarketplaceItems
            .Where(i => orderIds.Contains(i.OrderId) && i.Price != null && i.CurrencyCode != null)
            .GroupBy(i => new { i.Order.MarketplaceOrder!.MarketplaceAccountId, i.CurrencyCode })
            .Select(g => new AccountCurrencySum
            {
                AccountId = g.Key.MarketplaceAccountId,
                CurrencyCode = g.Key.CurrencyCode!,
                Amount = g.Sum(i => i.Price!.Value * i.Quantity),
            })
            .ToListAsync(ct);

        var priceByKey = price.ToDictionary(p => (p.AccountId, p.CurrencyCode), p => p.Amount);

        return net
            .Where(n => n.Amount > 0 && priceByKey.GetValueOrDefault((n.AccountId, n.CurrencyCode)) > 0)
            .GroupBy(n => n.AccountId)
            .ToDictionary(
                g => g.Key,
                g => g.ToDictionary(n => n.CurrencyCode, n => n.Amount / priceByKey[(n.AccountId, n.CurrencyCode)]));
    }

    /// <summary>Postings with a journal line dated in the period, or ever without one, per shop and currency.</summary>
    private async Task<Dictionary<(Guid AccountId, string CurrencyCode), int>> CountAccruedPostingsAsync(
        List<Guid> accountIds, DateOnly? from, DateOnly? to, CancellationToken ct) =>
        (await db.MarketplaceAccruals
            .Where(a => accountIds.Contains(a.MarketplaceAccountId)
                && a.OrderId != null
                && a.CurrencyCode != null
                && (from == null || a.Date >= from)
                && (to == null || a.Date <= to))
            .GroupBy(a => new { a.MarketplaceAccountId, a.CurrencyCode })
            .Select(g => new
            {
                g.Key.MarketplaceAccountId,
                CurrencyCode = g.Key.CurrencyCode!,
                Postings = g.Select(a => a.OrderId).Distinct().Count(),
            })
            .ToListAsync(ct))
        .ToDictionary(x => (x.MarketplaceAccountId, x.CurrencyCode), x => x.Postings);

    /// <summary>Every journal line dated in the period, or ever without one, by category and posting link.</summary>
    private Task<List<CategorySum>> LoadCategoriesAsync(
        List<Guid> accountIds, DateOnly? from, DateOnly? to, CancellationToken ct) =>
        db.MarketplaceAccruals
            .Where(a => accountIds.Contains(a.MarketplaceAccountId)
                && a.CurrencyCode != null
                && (from == null || a.Date >= from)
                && (to == null || a.Date <= to))
            .GroupBy(a => new
            {
                a.MarketplaceAccountId, a.CurrencyCode, a.Category, a.Source, ByPosting = a.OrderId != null,
            })
            .Select(g => new CategorySum
            {
                AccountId = g.Key.MarketplaceAccountId,
                CurrencyCode = g.Key.CurrencyCode!,
                Category = g.Key.Category,
                Source = g.Key.Source,
                ByPosting = g.Key.ByPosting,
                Amount = g.Sum(a => a.Amount),
            })
            .ToListAsync(ct);

    private static PayoutsMoneyDto BuildMoney(
        string currency,
        List<OpenPosting> open,
        decimal? ratio,
        int accruedPostings,
        List<PayoutsCategoryDto> categories,
        AnalyticsOptions options)
    {
        decimal? Estimate(IEnumerable<OpenPosting> postings) =>
            ratio is { } r ? Math.Round(postings.Sum(p => p.Amount) * r, 2) : null;

        var inTransit = open.Where(p => p.Bucket == Bucket.InTransit).ToList();
        var overdue = inTransit.Where(p => p.AgeDays >= options.PayoutOverdueDays).ToList();
        var notAccrued = open.Where(p => p.Bucket == Bucket.DeliveredNotAccrued).ToList();
        var byMarketplace = open.Where(p => p.Bucket == Bucket.NotAccruedByMarketplace).ToList();
        List<decimal> byAge = ratio is { } ageRatio
            ? AnalyticsCalculator
                .SplitByAge(inTransit.Select(p => (p.AgeDays, p.Amount)), options.PayoutAgeBoundaries)
                .Select(a => Math.Round(a * ageRatio, 2))
                .ToList()
            : [];

        return new PayoutsMoneyDto
        {
            CurrencyCode = currency,
            PayoutRatio = ratio is { } value ? (double)value : null,
            // Summed from the rounded buckets so the row adds up to the kopeck
            InTransit = ratio is null ? null : byAge.Sum(),
            InTransitPostings = inTransit.Count,
            InTransitByAge = byAge,
            InTransitOverdue = Estimate(overdue),
            InTransitOverduePostings = overdue.Count,
            DeliveredNotAccrued = Estimate(notAccrued),
            DeliveredNotAccruedPostings = notAccrued.Count,
            NotAccruedByMarketplace = Estimate(byMarketplace),
            NotAccruedByMarketplacePostings = byMarketplace.Count,
            Accrued = categories.Sum(x => x.Amount),
            AccruedPostings = accruedPostings,
            Categories = categories,
        };
    }

    private static List<PayoutsMoneyDto> BuildTotals(List<PayoutsRowDto> rows, int ageBuckets) =>
        rows
            .SelectMany(r => r.Money)
            .GroupBy(m => m.CurrencyCode)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g =>
            {
                var money = g.ToList();
                var estimated = money.Where(m => m.PayoutRatio != null).ToList();

                decimal? Sum(Func<PayoutsMoneyDto, decimal?> pick) =>
                    estimated.Count == 0 ? null : estimated.Sum(m => pick(m) ?? 0);

                return new PayoutsMoneyDto
                {
                    CurrencyCode = g.Key,
                    InTransit = Sum(m => m.InTransit),
                    InTransitPostings = money.Sum(m => m.InTransitPostings),
                    InTransitByAge = estimated.Count == 0
                        ? []
                        : Enumerable.Range(0, ageBuckets).Select(i => estimated.Sum(m => m.InTransitByAge[i])).ToList(),
                    InTransitOverdue = Sum(m => m.InTransitOverdue),
                    InTransitOverduePostings = money.Sum(m => m.InTransitOverduePostings),
                    DeliveredNotAccrued = Sum(m => m.DeliveredNotAccrued),
                    DeliveredNotAccruedPostings = money.Sum(m => m.DeliveredNotAccruedPostings),
                    NotAccruedByMarketplace = Sum(m => m.NotAccruedByMarketplace),
                    NotAccruedByMarketplacePostings = money.Sum(m => m.NotAccruedByMarketplacePostings),
                    Accrued = money.Sum(m => m.Accrued),
                    AccruedPostings = money.Sum(m => m.AccruedPostings),
                    Categories = money
                        .SelectMany(m => m.Categories)
                        .GroupBy(x => (x.Category, x.Source, x.ByPosting))
                        .Select(x => new PayoutsCategoryDto
                        {
                            Category = x.Key.Category,
                            Source = x.Key.Source,
                            ByPosting = x.Key.ByPosting,
                            Amount = x.Sum(y => y.Amount),
                        })
                        .ToList(),
                };
            })
            .ToList();
}
