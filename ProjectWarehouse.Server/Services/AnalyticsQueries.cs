using Microsoft.EntityFrameworkCore;
using ProjectWarehouse.Server.Data;
using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Infrastructure;
using ProjectWarehouse.Server.Models.Analytics;

namespace ProjectWarehouse.Server.Services;

/// <summary>The requested period and the one before it, cut into UTC half-open bounds in the caller's zone.</summary>
public sealed record AnalyticsPeriod(
    DateOnly From,
    DateOnly To,
    DateOnly PreviousFrom,
    DateOnly PreviousTo,
    DateTime FromUtc,
    DateTime ToUtc,
    DateTime PreviousFromUtc,
    DateOnly Today,
    int OffsetMinutes,
    string TimeZoneId);

public sealed record AnalyticsClock(DateOnly Today, int OffsetMinutes, string TimeZoneId);

public sealed record AnalyticsAccount(Guid Id, string Name, MarketplaceType Type);

/// <summary>
/// The building blocks every analytics report shares: the period, the selected shops and what counts as a
/// sale or a return. One definition here keeps the summary, the chart and the returns card agreeing on
/// the same orders.
/// </summary>
public class AnalyticsQueries(ApplicationDbContext db, IWarehouseTimeZoneResolver timeZones)
{
    /// <summary>A marketplace order is judged by the marketplace status only; WMS status does not matter.</summary>
    public static readonly MarketplaceOrderStatus[] MarketplaceSaleStatuses =
        [MarketplaceOrderStatus.Delivering, MarketplaceOrderStatus.Delivered];

    public static readonly MarketplaceOrderStatus[] MarketplaceCountedStatuses =
        [MarketplaceOrderStatus.Delivering, MarketplaceOrderStatus.Delivered, MarketplaceOrderStatus.Cancelled];

    public static readonly OrderStatus[] DirectSaleStatuses = [OrderStatus.Assembled, OrderStatus.Shipped];

    public static readonly OrderStatus[] DirectCountedStatuses =
        [OrderStatus.Assembled, OrderStatus.Shipped, OrderStatus.Canceled];

    /// <exception cref="ValidationException">The period is inverted or longer than allowed.</exception>
    public async Task<AnalyticsPeriod> ResolvePeriodAsync(AnalyticsFilterRequest request, CancellationToken ct)
    {
        var from = request.From!.Value;
        var to = request.To!.Value;

        if (from > to)
            throw new ValidationException("to", ErrorCode.InvalidValue,
                "The end of the period must not be earlier than its start.");

        if (to.DayNumber - from.DayNumber + 1 > AnalyticsCalculator.MaxPeriodDays)
            throw new ValidationException("to", ErrorCode.OutOfRange,
                $"The period must not exceed {AnalyticsCalculator.MaxPeriodDays} days.");

        var clock = await ResolveClockAsync(ct);
        var offset = TimeSpan.FromMinutes(clock.OffsetMinutes);
        var (previousFrom, previousTo) = AnalyticsCalculator.PreviousPeriod(from, to);

        return new AnalyticsPeriod(
            from,
            to,
            previousFrom,
            previousTo,
            ToUtc(from, offset),
            ToUtc(to.AddDays(1), offset),
            ToUtc(previousFrom, offset),
            clock.Today,
            clock.OffsetMinutes,
            clock.TimeZoneId);
    }

    /// <summary>Today and the offset in the caller's zone, for a report that has no period of its own.</summary>
    public async Task<AnalyticsClock> ResolveClockAsync(CancellationToken ct)
    {
        var zone = await timeZones.ResolveAsync(null, ct);
        var offsetMinutes = zone.CurrentOffsetMinutes();
        return new AnalyticsClock(
            DateOnly.FromDateTime(DateTime.UtcNow + TimeSpan.FromMinutes(offsetMinutes)),
            offsetMinutes,
            zone.IanaId());
    }

    /// <summary>The shops the filter selects, by name; empty when marketplaces are switched off.</summary>
    public Task<List<AnalyticsAccount>> LoadAccountsAsync(AnalyticsFilterRequest request, CancellationToken ct)
    {
        var includeMarketplaces = request.IncludeMarketplaces;
        var accountIds = request.MarketplaceAccountIds is { Length: > 0 } ids ? ids : null;

        return db.MarketplaceAccounts
            .Where(a => includeMarketplaces && (accountIds == null || accountIds.Contains(a.Id)))
            .OrderBy(a => a.Name)
            .Select(a => new AnalyticsAccount(a.Id, a.Name, a.Type))
            .ToListAsync(ct);
    }

    public IQueryable<Order> MarketplaceOrders(
        List<Guid> accountIds, MarketplaceOrderStatus[] statuses, DateTime fromUtc, DateTime toUtc) =>
        db.Orders.Where(o => o.MarketplaceOrder != null
            && accountIds.Contains(o.MarketplaceOrder.MarketplaceAccountId)
            && statuses.Contains(o.MarketplaceOrder.Status)
            && o.EffectiveDate >= fromUtc && o.EffectiveDate < toUtc);

    /// <summary>Lines of the marketplace sales dated inside the bounds.</summary>
    public IQueryable<OrderMarketplaceItem> SaleLines(List<Guid> accountIds, DateTime fromUtc, DateTime toUtc) =>
        db.OrderMarketplaceItems.Where(i => i.Order.MarketplaceOrder != null
            && accountIds.Contains(i.Order.MarketplaceOrder.MarketplaceAccountId)
            && MarketplaceSaleStatuses.Contains(i.Order.MarketplaceOrder.Status)
            && i.Order.EffectiveDate >= fromUtc && i.Order.EffectiveDate < toUtc);

    /// <summary>
    /// Journal lines of the accrued sales dated inside the bounds — what the payout money is summed from. A sale
    /// not accrued yet brings none of its lines, or a fee charged ahead of the sale would read as a loss.
    /// </summary>
    public IQueryable<MarketplaceAccrual> SaleAccruals(List<Guid> accountIds, DateTime fromUtc, DateTime toUtc) =>
        db.MarketplaceAccruals.Where(a => a.CurrencyCode != null
            && a.Order != null
            && a.Order.MarketplaceOrder != null
            && accountIds.Contains(a.Order.MarketplaceOrder.MarketplaceAccountId)
            && MarketplaceSaleStatuses.Contains(a.Order.MarketplaceOrder.Status)
            && a.Order.EffectiveDate >= fromUtc && a.Order.EffectiveDate < toUtc
            && a.Order.IsAccrued);

    /// <summary>
    /// Returns of the sales dated inside the bounds, whenever the item came back — the cohort a return share
    /// is taken over.
    /// </summary>
    public IQueryable<MarketplaceReturn> CohortReturns(List<Guid> accountIds, DateTime fromUtc, DateTime toUtc) =>
        SaleReturns(accountIds)
            .Where(r => r.Order!.EffectiveDate >= fromUtc && r.Order.EffectiveDate < toUtc);

    /// <summary>Returns of any sale that came back inside the bounds.</summary>
    public IQueryable<MarketplaceReturn> ReturnsByReturnDate(
        List<Guid> accountIds, DateTime fromUtc, DateTime toUtc) =>
        SaleReturns(accountIds).Where(r => r.ReturnedAt >= fromUtc && r.ReturnedAt < toUtc);

    /// <param name="tagIds">Keeps orders carrying any of them; empty keeps every Direct order.</param>
    /// <param name="statuses">WMS statuses to keep.</param>
    /// <param name="fromUtc">Inclusive lower bound of <c>EffectiveDate</c>.</param>
    /// <param name="toUtc">Exclusive upper bound of <c>EffectiveDate</c>.</param>
    public IQueryable<Order> DirectOrders(Guid[]? tagIds, OrderStatus[] statuses, DateTime fromUtc, DateTime toUtc)
    {
        var query = db.Orders.Where(o => o.Type == OrderType.Direct
            && statuses.Contains(o.Status)
            && o.EffectiveDate >= fromUtc && o.EffectiveDate < toUtc);

        return tagIds is { Length: > 0 } ? query.Where(o => o.Tags.Any(t => tagIds.Contains(t.Id))) : query;
    }

    /// <summary>
    /// A return counts when the buyer really sent the item back and it belongs to a sale; one without an
    /// order has neither a channel nor a sale to belong to.
    /// </summary>
    private IQueryable<MarketplaceReturn> SaleReturns(List<Guid> accountIds) =>
        db.MarketplaceReturns.Where(r => r.IsCountedAsReturn
            && r.Order != null
            && r.Order.MarketplaceOrder != null
            && accountIds.Contains(r.Order.MarketplaceOrder.MarketplaceAccountId)
            && MarketplaceSaleStatuses.Contains(r.Order.MarketplaceOrder.Status));

    /// <summary>Start of <paramref name="day"/> at the given zone offset, as UTC.</summary>
    public static DateTime ToUtc(DateOnly day, TimeSpan offset) =>
        DateTime.SpecifyKind(day.ToDateTime(TimeOnly.MinValue) - offset, DateTimeKind.Utc);
}
