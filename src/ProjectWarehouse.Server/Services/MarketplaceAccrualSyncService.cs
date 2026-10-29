using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ProjectWarehouse.Server.Data;
using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Infrastructure;
using ProjectWarehouse.Server.Infrastructure.Marketplaces;
using ProjectWarehouse.Server.Infrastructure.Observability;
using ProjectWarehouse.Server.Infrastructure.Realtime;
using ProjectWarehouse.Server.Integrations.Abstractions;
using ProjectWarehouse.Server.Integrations.Sync;
using ProjectWarehouse.Server.Models;

namespace ProjectWarehouse.Server.Services;

public class MarketplaceAccrualSyncService(
    ApplicationDbContext db,
    IRealtimeNotifier realtime,
    IOptions<MarketplacesOptions> options) : IMarketplaceAccrualSyncService
{
    private const int RelinkBatchSize = 500;

    /// <summary>Scopes whose unit number can name a posting; container fees are about a supply.</summary>
    private static readonly MarketplaceAccrualScope[] LinkableScopes =
        [MarketplaceAccrualScope.Posting, MarketplaceAccrualScope.Item, MarketplaceAccrualScope.Account];

    /// <summary>
    /// Scopes whose unit number can also name an order. A shop-level unit is a contract or a campaign as often
    /// as a posting, so it links only by an exact posting number.
    /// </summary>
    private static readonly MarketplaceAccrualScope[] OrderNumberScopes =
        [MarketplaceAccrualScope.Posting, MarketplaceAccrualScope.Item];

    /// <summary>Journal days are the marketplace's accounting days, which for Ozon are Moscow days.</summary>
    private static readonly TimeZoneInfo JournalZone =
        TimeZoneInfo.TryFindSystemTimeZoneById("Europe/Moscow", out var zone)
            ? zone
            : TimeZoneInfo.CreateCustomTimeZone("MSK", TimeSpan.FromHours(3), "MSK", "MSK");

    private readonly OzonOptions _ozon = options.Value.Ozon;

    public async Task SyncAccrualsAsync(IMarketplaceProvider provider, MarketplaceCredentials credentials,
        MarketplaceAccount account, MarketplaceSyncRun run, CancellationToken ct)
    {
        if (!provider.Capabilities.HasFlag(MarketplaceCapabilities.Accruals))
            return;

        using var activity = AppTelemetry.Source.StartActivity("marketplace.sync.accruals");

        var now = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(now);
        var fullPass = account.AccrualsFullPassAt is not { } fullPassAt
                       || fullPassAt <= now.AddHours(-_ozon.AccrualsFullPassIntervalHours);

        // the day before the last run, so a run cut short at midnight or a paused account leaves no gap
        var from = account.AccrualsSyncedAt is { } syncedAt
            ? DateOnly.FromDateTime(syncedAt).AddDays(-1)
            : today.AddDays(-_ozon.AccrualsImportWindowPastDays);
        var overlapStart = today.AddDays(-_ozon.AccrualsOverlapDays);
        if (fullPass && overlapStart < from)
            from = overlapStart;

        // a day ahead of UTC: the marketplace's day may already have started, the provider cuts off its future
        await ImportAsync(provider.FetchAccrualsAsync(credentials, from, today.AddDays(1), ct), account, run, ct);

        // only once the whole period is through, for the same reason as FboPostingsSyncedAt
        account.AccrualsSyncedAt = now;
        if (fullPass)
            account.AccrualsFullPassAt = now;
        await db.SaveChangesAsync(ct);

        if (provider.Capabilities.HasFlag(MarketplaceCapabilities.Buyouts) && account.BuyoutsSyncEnabled)
            await SyncBuyoutsAsync(provider, credentials, account, run, from, today.AddDays(1), now, ct);

        await RelinkAsync(account.Id, run, ct);
        if (provider.Capabilities.HasFlag(MarketplaceCapabilities.ReversedSaleReturns))
            await ApplyReversedSalesAsync(account.Id, run, ct);
        await BackfillDeliveredAtAsync(account.Id, run, ct);

        activity?.SetTag("marketplace.accruals.full_pass", fullPass);
        activity?.SetTag("marketplace.accruals.created", run.AccrualsCreated);
        activity?.SetTag("marketplace.accruals.updated", run.AccrualsUpdated);
    }

    public async Task SyncAccrualsBackfillAsync(IMarketplaceProvider provider, MarketplaceCredentials credentials,
        MarketplaceAccount account, MarketplaceSyncRun run, DateTime since, DateTime to, CancellationToken ct)
    {
        if (!provider.Capabilities.HasFlag(MarketplaceCapabilities.Accruals))
            return;

        using var activity = AppTelemetry.Source.StartActivity("marketplace.sync.accruals_backfill");

        var from = DateOnly.FromDateTime(since);
        var till = DateOnly.FromDateTime(to);
        await ImportAsync(provider.FetchAccrualsAsync(credentials, from, till, ct), account, run, ct);
        if (provider.Capabilities.HasFlag(MarketplaceCapabilities.Buyouts) && account.BuyoutsSyncEnabled
            && await ImportBuyoutsAsync(provider, credentials, account, run, from, till, int.MaxValue, ct)
                is { } refused)
            WarnBuyoutsRefused(run, refused);
        await RelinkAsync(account.Id, run, ct);
        if (provider.Capabilities.HasFlag(MarketplaceCapabilities.ReversedSaleReturns))
            await ApplyReversedSalesAsync(account.Id, run, ct);
        await BackfillDeliveredAtAsync(account.Id, run, ct);

        activity?.SetTag("marketplace.accruals.created", run.AccrualsCreated);
    }

    /// <summary>
    /// The latest buyouts, from the journal's first day of this run or from where the last read stopped, oldest
    /// window first, then one window of history below <see cref="MarketplaceAccount.BuyoutsLoadedFrom"/>. The
    /// report's quota is tiny and lasts hours, so a refusal ends the step instead of the run.
    /// </summary>
    private async Task SyncBuyoutsAsync(IMarketplaceProvider provider, MarketplaceCredentials credentials,
        MarketplaceAccount account, MarketplaceSyncRun run, DateOnly from, DateOnly to, DateTime now,
        CancellationToken ct)
    {
        if (account.BuyoutsSyncedAt is { } syncedAt && DateOnly.FromDateTime(syncedAt).AddDays(-1) < from)
            from = DateOnly.FromDateTime(syncedAt).AddDays(-1);

        if (await ImportBuyoutsAsync(provider, credentials, account, run, from, to, int.MaxValue, ct,
                oldestFirst: true) is { } refused)
        {
            WarnBuyoutsRefused(run, refused);
            return;
        }

        // the last window already moved the mark to about now; this only covers a read with no window, on a span
        if (account.BuyoutsLoadedFrom is not null && (account.BuyoutsSyncedAt is not { } read || read < now))
        {
            account.BuyoutsSyncedAt = now;
            await db.SaveChangesAsync(ct);
        }

        var journalStart = await db.MarketplaceAccruals
            .Where(a => a.MarketplaceAccountId == account.Id && a.Source == MarketplaceAccrualSource.AccrualJournal)
            .MinAsync(a => (DateOnly?)a.Date, ct);
        if (journalStart is not { } start || account.BuyoutsLoadedFrom is not { } loaded || loaded <= start)
            return;

        // history is not worth a warning; its 429 still sets the pause
        await ImportBuyoutsAsync(provider, credentials, account, run, start, loaded.AddDays(-1), 1, ct,
            followsBuyoutCall: true);
    }

    /// <summary>
    /// Reads at most <paramref name="maxWindows"/> report windows, newest back unless <paramref name="oldestFirst"/>.
    /// Newest back, a window joining the loaded span moves <see cref="MarketplaceAccount.BuyoutsLoadedFrom"/> down;
    /// oldest first, it widens the span on both ends, and one apart from it replaces the span. Null when
    /// all were read, else why not: a 429 now, or <see cref="MarketplaceAccount.BuyoutsPausedUntil"/> from an
    /// earlier one; what was read by then is saved.
    /// </summary>
    private async Task<ErrorCode?> ImportBuyoutsAsync(IMarketplaceProvider provider,
        MarketplaceCredentials credentials, MarketplaceAccount account, MarketplaceSyncRun run, DateOnly from,
        DateOnly to, int maxWindows, CancellationToken ct, bool followsBuyoutCall = false, bool oldestFirst = false)
    {
        if (account.BuyoutsPausedUntil > DateTime.UtcNow)
            return ErrorCode.MarketplaceBuyoutsPaused;

        try
        {
            var read = 0;
            await foreach (var window in provider.FetchBuyoutsAsync(credentials, from, to, followsBuyoutCall,
                               oldestFirst, ct))
            {
                await ImportPageAsync(window.Accruals, account, run, ct);

                // after the rows are in, or a failed page would leave the mark past a window never stored
                if (oldestFirst)
                {
                    var past = window.To.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
                    var utcNow = DateTime.UtcNow;
                    if (past > utcNow)
                        past = utcNow;

                    // a window joining the span widens it; one apart from it starts the span anew. The mark is
                    // compared as a Moscow day, the windows' own, or a mark capped at now would part adjacent days
                    if (account is { BuyoutsLoadedFrom: { } start, BuyoutsSyncedAt: { } synced }
                        && window.From <= DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(synced, JournalZone))
                        && window.To >= start.AddDays(-1))
                    {
                        if (window.From < start)
                            account.BuyoutsLoadedFrom = window.From;
                        if (past > synced)
                            account.BuyoutsSyncedAt = past;
                    }
                    else
                    {
                        account.BuyoutsLoadedFrom = window.From;
                        account.BuyoutsSyncedAt = past;
                    }

                    await db.SaveChangesAsync(ct);
                }
                else if (account.BuyoutsLoadedFrom is { } loaded && window.From < loaded
                         && window.To >= loaded.AddDays(-1))
                {
                    account.BuyoutsLoadedFrom = window.From;
                    await db.SaveChangesAsync(ct);
                }

                if (++read >= maxWindows)
                    break;
            }

            return null;
        }
        catch (MarketplaceApiException ex) when (ex.StatusCode == StatusCodes.Status429TooManyRequests)
        {
            account.BuyoutsPausedUntil = BuyoutsPauseEnd(DateTime.UtcNow);
            await db.SaveChangesAsync(ct);
            return ErrorCode.MarketplaceBuyoutsRateLimited;
        }
    }

    /// <summary>The next reset hour in Moscow, but no sooner than the minimal pause.</summary>
    private DateTime BuyoutsPauseEnd(DateTime utcNow)
    {
        var local = TimeZoneInfo.ConvertTimeFromUtc(utcNow, JournalZone);
        var reset = local.Date.AddHours(_ozon.BuyoutsPauseResetHour);
        if (reset <= local)
            reset = reset.AddDays(1);
        var resetUtc =
            TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(reset, DateTimeKind.Unspecified), JournalZone);
        var minimal = utcNow.AddMinutes(_ozon.BuyoutsMinPauseMinutes);
        return resetUtc > minimal ? resetUtc : minimal;
    }

    private static void WarnBuyoutsRefused(MarketplaceSyncRun run, ErrorCode code) =>
        run.Warning = new AppFieldError
        {
            Code = code,
            Detail = code == ErrorCode.MarketplaceBuyoutsPaused
                ? "The buyout report is paused after an earlier 429; later runs read the rest."
                : "The marketplace refused the buyout report with a 429; later runs read the rest.",
        };

    private async Task ImportAsync(IAsyncEnumerable<IReadOnlyList<ExternalAccrual>> pages,
        MarketplaceAccount account, MarketplaceSyncRun run, CancellationToken ct)
    {
        await foreach (var fetched in pages)
            await ImportPageAsync(fetched, account, run, ct);
    }

    /// <summary>Writes one page and saves it together with whatever else the account has pending.</summary>
    private async Task ImportPageAsync(IReadOnlyList<ExternalAccrual> fetched, MarketplaceAccount account,
        MarketplaceSyncRun run, CancellationToken ct)
    {
        run.AccrualsProcessed += fetched.Count;

        var ids = fetched.Select(a => a.ExternalId).Distinct().ToList();
        var known = (await db.MarketplaceAccruals
                .Where(a => a.MarketplaceAccountId == account.Id && ids.Contains(a.ExternalId))
                .ToListAsync(ct))
            .GroupBy(a => a.ExternalId)
            .ToDictionary(g => g.Key, g => g.OrderBy(a => a.LineNo).ToList());

        var page = await DateBuyoutsAsync(account.Id, fetched, known, ct);
        var changed = page.Where(a => !known.TryGetValue(a.ExternalId, out var rows) || !Matches(rows, a)).ToList();
        var linkable = changed.Where(a => LinkableScopes.Contains(a.Scope)).ToList();
        var orders = await LoadOrdersAsync(account.Id,
            linkable.Select(a => a.UnitNumber).OfType<string>().ToHashSet(),
            linkable.Select(a => a.Rid).OfType<string>().ToHashSet(),
            ct);

        var now = DateTime.UtcNow;
        foreach (var external in changed)
        {
            var existing = known.GetValueOrDefault(external.ExternalId) ?? [];
            // a row per line; LineNo keeps each in place, so an edited accrual updates rather than re-inserts
            var rows = Replace(account.Id, existing, external, now);

            foreach (var row in rows)
                TryLink(row, orders);

            if (existing.Count == 0)
                run.AccrualsCreated++;
            else
                run.AccrualsUpdated++;

            // an accrual listed twice in one page must compare against its second sighting, not insert again
            known[external.ExternalId] = rows;
        }

        await db.SaveChangesAsync(ct);
        await realtime.PublishProgressAsync(run, ct);
    }

    /// <summary>
    /// Rewrites the rows of <paramref name="existing"/> in place with the lines of <paramref name="source"/>,
    /// adding or deleting rows where the count differs. Links are cleared and resolved again by the caller:
    /// the unit or SKU may be the very thing that changed.
    /// </summary>
    private List<MarketplaceAccrual> Replace(Guid accountId, List<MarketplaceAccrual> existing,
        ExternalAccrual source, DateTime now)
    {
        var rows = new List<MarketplaceAccrual>(source.Lines.Count);

        for (var i = 0; i < source.Lines.Count; i++)
        {
            var row = i < existing.Count ? existing[i] : null;
            if (row is null)
            {
                row = new MarketplaceAccrual
                {
                    Id = Guid.NewGuid(),
                    MarketplaceAccountId = accountId,
                    ExternalId = source.ExternalId,
                    LineNo = i,
                };
                db.MarketplaceAccruals.Add(row);
            }

            var line = source.Lines[i];
            row.Date = source.Date;
            row.Scope = source.Scope;
            row.Source = source.Source;
            row.UnitNumber = source.UnitNumber;
            row.Rid = source.Rid;
            row.ExemplarId = source.ExemplarId;
            row.Category = line.Category;
            row.RawTypeId = line.RawTypeId;
            row.Sku = line.Sku;
            row.Amount = line.Amount;
            row.CurrencyCode = line.CurrencyCode;
            row.OrderId = null;
            row.OrderMarketplaceItemId = null;
            row.CatalogItemId = null;
            row.SyncedAt = now;
            rows.Add(row);
        }

        db.MarketplaceAccruals.RemoveRange(existing.Skip(source.Lines.Count));
        return rows;
    }

    /// <summary>
    /// True when the stored rows say exactly what the marketplace says now. An unchanged accrual is left alone,
    /// or the daily re-read of two weeks would rewrite every row of them.
    /// </summary>
    private static bool Matches(List<MarketplaceAccrual> rows, ExternalAccrual source) =>
        rows.Count == source.Lines.Count
        && rows.Zip(source.Lines).All(p =>
            p.First.Date == source.Date
            && p.First.Scope == source.Scope
            && p.First.Source == source.Source
            && p.First.UnitNumber == source.UnitNumber
            && p.First.Rid == source.Rid
            && p.First.ExemplarId == source.ExemplarId
            && p.First.Category == p.Second.Category
            && p.First.RawTypeId == p.Second.RawTypeId
            && p.First.Sku == p.Second.Sku
            && p.First.Amount == p.Second.Amount
            && p.First.CurrencyCode == p.Second.CurrencyCode);

    /// <summary>
    /// The buyout report dates no row. A buyout takes the Moscow day its posting was delivered, else the day
    /// it is already stored on, else the provider's window end — so a later window does not move it again.
    /// </summary>
    private async Task<IReadOnlyList<ExternalAccrual>> DateBuyoutsAsync(Guid accountId,
        IReadOnlyList<ExternalAccrual> page, Dictionary<string, List<MarketplaceAccrual>> known, CancellationToken ct)
    {
        var units = page
            .Where(a => a.Source == MarketplaceAccrualSource.BuyoutReport)
            .Select(a => a.UnitNumber)
            .OfType<string>()
            .ToHashSet();
        if (units.Count == 0)
            return page;

        var delivered = await db.MarketplaceOrders
            .Where(o => o.MarketplaceAccountId == accountId && units.Contains(o.PostingNumber) && o.DeliveredAt != null)
            .ToDictionaryAsync(o => o.PostingNumber, o => o.DeliveredAt!.Value, ct);

        return page
            .Select(a =>
            {
                if (a.Source != MarketplaceAccrualSource.BuyoutReport)
                    return a;
                if (a.UnitNumber is { } unit && delivered.TryGetValue(unit, out var at))
                    return a with { Date = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(at, JournalZone)) };
                if (known.TryGetValue(a.ExternalId, out var rows) && rows.Count > 0)
                    return a with { Date = rows[0].Date };
                return a;
            })
            .ToList();
    }

    /// <summary>
    /// A posting whose journal sale was reversed in full was bought and given back, however the marketplace closed
    /// it: the unit becomes a counted customer return, keyed by its exemplar so the returns report lands on the same
    /// row, and a cancelled posting becomes Delivered with its cancellation cleared. Its delivery day is then the
    /// sale's, set by <see cref="BackfillDeliveredAtAsync"/>. Postings already carrying such a return are skipped.
    /// </summary>
    private async Task ApplyReversedSalesAsync(Guid accountId, MarketplaceSyncRun run, CancellationToken ct)
    {
        var reversed = await db.MarketplaceOrders
            .Where(o => o.MarketplaceAccountId == accountId
                        && (o.Status == MarketplaceOrderStatus.Cancelled
                            || o.Status == MarketplaceOrderStatus.Delivered)
                        && db.MarketplaceAccruals.Any(a => a.OrderId == o.OrderId
                                                           && a.Source == MarketplaceAccrualSource.AccrualJournal
                                                           && a.Category == MarketplaceAccrualCategory.Sale
                                                           && a.Amount < 0)
                        && !db.MarketplaceReturns.Any(r => r.OrderId == o.OrderId && r.SaleReversedOn != null))
            .Select(o => new
            {
                o.OrderId,
                o.PostingNumber,
                o.Status,
                o.RawSubstatus,
                o.LabelBarcode,
                Net = db.MarketplaceAccruals
                    .Where(a => a.OrderId == o.OrderId
                                && a.Source == MarketplaceAccrualSource.AccrualJournal
                                && a.Category == MarketplaceAccrualCategory.Sale)
                    .Sum(a => a.Amount),
                Sale = db.MarketplaceAccruals
                    .Where(a => a.OrderId == o.OrderId
                                && a.Source == MarketplaceAccrualSource.AccrualJournal
                                && a.Category == MarketplaceAccrualCategory.Sale
                                && a.Amount > 0)
                    .OrderBy(a => a.Date)
                    .Select(a => new
                    {
                        a.Amount,
                        a.CurrencyCode,
                        a.Sku,
                        a.OrderMarketplaceItemId,
                        a.CatalogItemId,
                        OfferId = a.OrderMarketplaceItem!.MarketplaceCard!.OfferId,
                    })
                    .FirstOrDefault(),
                Reversal = db.MarketplaceAccruals
                    .Where(a => a.OrderId == o.OrderId
                                && a.Source == MarketplaceAccrualSource.AccrualJournal
                                && a.Category == MarketplaceAccrualCategory.Sale
                                && a.Amount < 0)
                    .OrderByDescending(a => a.Date)
                    .Select(a => new { a.Date, a.ExemplarId })
                    .FirstOrDefault(),
            })
            .Where(x => x.Sale != null && x.Net <= 0)
            .ToListAsync(ct);

        // the exemplar comes with rows written since it was stored; older ones fall back to the archived sticker
        var units = reversed
            .Select(x => (Posting: x, Exemplar: x.Reversal!.ExemplarId ?? x.LabelBarcode))
            .Where(x => x.Exemplar is not null)
            .ToList();

        var exemplars = units.Select(x => x.Exemplar!).ToList();
        var known = await db.MarketplaceReturns
            .Where(r => r.MarketplaceAccountId == accountId && exemplars.Contains(r.ExternalId))
            .ToDictionaryAsync(r => r.ExternalId, ct);

        var now = DateTime.UtcNow;
        foreach (var (posting, exemplar) in units)
        {
            var sale = posting.Sale!;
            var reversedOn = posting.Reversal!.Date;
            var defect = posting.RawSubstatus == "defect" ? MarketplaceLossReason.Defect : (MarketplaceLossReason?)null;

            if (!known.TryGetValue(exemplar!, out var target))
            {
                target = new MarketplaceReturn
                {
                    Id = Guid.NewGuid(),
                    MarketplaceAccountId = accountId,
                    ExternalId = exemplar!,
                    PostingNumber = posting.PostingNumber,
                    OfferId = sale.OfferId ?? "",
                    Scheme = MarketplaceReturnScheme.Fbs,
                    // an exemplar is a single unit
                    Quantity = 1,
                    Price = sale.Amount,
                    CurrencyCode = sale.CurrencyCode,
                    ReasonCategory = defect,
                    StatusChangedAt = TimeZoneInfo.ConvertTimeToUtc(reversedOn.ToDateTime(TimeOnly.MinValue), JournalZone),
                    ReturnedAt = TimeZoneInfo.ConvertTimeToUtc(reversedOn.ToDateTime(TimeOnly.MinValue), JournalZone),
                    ExemplarId = exemplar,
                    SyncedAt = now,
                };
                db.MarketplaceReturns.Add(target);
                known[exemplar!] = target;
                run.ReturnsCreated++;
            }
            else if (target.OrderId is { } linked && linked != posting.OrderId)
            {
                // a sticker reused by another task; the return stays with the posting it was linked to
                continue;
            }
            else
            {
                run.ReturnsUpdated++;
            }

            if (target.OrderId is null)
            {
                target.OrderId = posting.OrderId;
                target.OrderMarketplaceItemId = sale.OrderMarketplaceItemId;
                target.CatalogItemId = sale.CatalogItemId;
                target.PostingNumber = posting.PostingNumber;
                target.Scheme = MarketplaceReturnScheme.Fbs;
                target.Sku ??= sale.Sku;
            }
            target.Kind = MarketplaceReturnKind.CustomerReturn;
            target.IsCancelled = false;
            target.SaleReversedOn = reversedOn;
            target.Price ??= sale.Amount;
            target.CurrencyCode ??= sale.CurrencyCode;
            target.ReasonCategory ??= defect;
        }
        await db.SaveChangesAsync(ct);

        // by the stored returns rather than this pass's finds, so a pass cut short is finished by the next
        await db.MarketplaceOrders
            .Where(o => o.MarketplaceAccountId == accountId
                        && o.Status == MarketplaceOrderStatus.Cancelled
                        && db.MarketplaceReturns.Any(r => r.OrderId == o.OrderId && r.SaleReversedOn != null))
            .ExecuteUpdateAsync(s => s
                .SetProperty(o => o.Status, MarketplaceOrderStatus.Delivered)
                .SetProperty(o => o.DeliveredAt, (DateTime?)null)
                .SetProperty(o => o.CancelledAt, (DateTime?)null)
                .SetProperty(o => o.CancelledAfterAssembly, (bool?)null)
                .SetProperty(o => o.CancellationType, (MarketplaceCancellationType?)null)
                .SetProperty(o => o.RawCancellationType, (string?)null)
                .SetProperty(o => o.CancelReason, (string?)null)
                .SetProperty(o => o.CancelReasonCategory, (MarketplaceLossReason?)null)
                .SetProperty(o => o.RawCancelReasonCode, (string?)null), ct);

        // an external order follows its posting: the goods left, so it was shipped
        var unshipped = await db.Orders
            .Include(o => o.MarketplaceOrder)
            .Where(o => o.IsExternal
                        && o.Status == OrderStatus.Canceled
                        && o.MarketplaceOrder!.MarketplaceAccountId == accountId
                        && o.MarketplaceOrder.Status == MarketplaceOrderStatus.Delivered
                        && db.MarketplaceReturns.Any(r => r.OrderId == o.Id && r.SaleReversedOn != null))
            .ToListAsync(ct);
        foreach (var order in unshipped)
        {
            order.Status = OrderStatus.Shipped;
            order.ShippedAt = order.MarketplaceOrder?.InProcessAt ?? order.CreatedAt;
        }
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Delivered postings the order sync never saw turn Delivered — imported already delivered — take the Moscow
    /// day of their first journal sale, which Ozon books on the delivery day; a buyout, which has no sale in the
    /// journal, takes the day of its latest fee. Any other posting waits for its sale: a fee may be a later return
    /// or correction, and the day once set is never revisited. Buyouts are then moved onto their posting's day.
    /// </summary>
    private async Task BackfillDeliveredAtAsync(Guid accountId, MarketplaceSyncRun run, CancellationToken ct)
    {
        var days = await db.MarketplaceOrders
            .Where(o => o.MarketplaceAccountId == accountId
                        && o.Status == MarketplaceOrderStatus.Delivered
                        && o.DeliveredAt == null)
            .Select(o => new
            {
                o.OrderId,
                Sale = db.MarketplaceAccruals
                    .Where(a => a.OrderId == o.OrderId
                                && a.Source == MarketplaceAccrualSource.AccrualJournal
                                && a.Category == MarketplaceAccrualCategory.Sale
                                && a.Amount > 0)
                    .Min(a => (DateOnly?)a.Date),
                LastFee = db.MarketplaceAccruals
                    .Where(a => a.OrderId == o.OrderId
                                && a.Source == MarketplaceAccrualSource.AccrualJournal
                                && a.Category != MarketplaceAccrualCategory.Sale
                                && db.MarketplaceAccruals.Any(b => b.OrderId == o.OrderId
                                                                   && b.Source == MarketplaceAccrualSource.BuyoutReport))
                    .Max(a => (DateOnly?)a.Date),
            })
            .Where(x => x.Sale != null || x.LastFee != null)
            .ToListAsync(ct);

        foreach (var day in days.GroupBy(x => (x.Sale ?? x.LastFee)!.Value))
        {
            var ids = day.Select(x => x.OrderId).ToList();
            var at = TimeZoneInfo.ConvertTimeToUtc(day.Key.ToDateTime(TimeOnly.MinValue), JournalZone);
            await db.MarketplaceOrders
                .Where(o => ids.Contains(o.OrderId) && o.DeliveredAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(o => o.DeliveredAt, at), ct);
        }

        var buyouts = await db.MarketplaceAccruals
            .Where(a => a.MarketplaceAccountId == accountId
                        && a.Source == MarketplaceAccrualSource.BuyoutReport
                        && a.OrderId != null)
            .Select(a => new
            {
                a.Id,
                a.ExternalId,
                a.Date,
                DeliveredAt = db.MarketplaceOrders
                    .Where(o => o.OrderId == a.OrderId)
                    .Select(o => o.DeliveredAt)
                    .FirstOrDefault(),
            })
            .Where(x => x.DeliveredAt != null)
            .ToListAsync(ct);

        var moved = buyouts
            .Select(x => (x.Id, x.ExternalId, x.Date,
                Day: DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(x.DeliveredAt!.Value, JournalZone))))
            .Where(x => x.Date != x.Day)
            .ToList();

        foreach (var day in moved.GroupBy(x => x.Day))
        {
            var ids = day.Select(x => x.Id).ToList();
            await db.MarketplaceAccruals
                .Where(a => ids.Contains(a.Id))
                .ExecuteUpdateAsync(s => s.SetProperty(a => a.Date, day.Key), ct);
        }

        run.AccrualsUpdated += moved.Select(x => x.ExternalId).Distinct().Count();
    }

    /// <summary>
    /// Accruals saved before their posting was imported, which the order sync catches up with on its own
    /// interval, and those of a draft whose lines the order sync rebuilt, or whose draft it deleted. Only rows whose
    /// unit is known as a posting or an order now are loaded.
    /// </summary>
    private async Task RelinkAsync(Guid accountId, MarketplaceSyncRun run, CancellationToken ct)
    {
        var pending = await db.MarketplaceAccruals
            .Where(a => a.MarketplaceAccountId == accountId
                        && a.OrderId == null
                        && (a.UnitNumber != null || a.Rid != null)
                        && LinkableScopes.Contains(a.Scope)
                        && db.MarketplaceOrders.Any(o => o.MarketplaceAccountId == accountId
                                                         && (o.PostingNumber == a.UnitNumber
                                                             || (a.Rid != null && o.Rid == a.Rid)
                                                             || (OrderNumberScopes.Contains(a.Scope)
                                                                 && o.ExternalOrderNumber == a.UnitNumber))))
            .ToListAsync(ct);

        foreach (var batch in pending.Chunk(RelinkBatchSize))
        {
            var orders = await LoadOrdersAsync(accountId,
                batch.Select(a => a.UnitNumber).OfType<string>().ToHashSet(),
                batch.Select(a => a.Rid).OfType<string>().ToHashSet(),
                ct);

            var linked = batch.Where(a => TryLink(a, orders)).Select(a => a.ExternalId).Distinct().Count();
            run.AccrualsUpdated += linked;

            await db.SaveChangesAsync(ct);
        }
    }

    /// <summary>Postings named by a unit number either directly or through the order they belong to, or by rid.</summary>
    private async Task<UnitIndex> LoadOrdersAsync(Guid accountId, IReadOnlyCollection<string> units,
        IReadOnlyCollection<string> rids, CancellationToken ct)
    {
        if (units.Count == 0 && rids.Count == 0)
            return UnitIndex.Empty;

        var postings = await db.MarketplaceOrders
            .Where(o => o.MarketplaceAccountId == accountId
                        && (units.Contains(o.PostingNumber)
                            || (o.ExternalOrderNumber != null && units.Contains(o.ExternalOrderNumber))
                            || (o.Rid != null && rids.Contains(o.Rid))))
            .OrderBy(o => o.PostingNumber)
            .Select(o => new PostingRow(
                o.PostingNumber,
                o.ExternalOrderNumber,
                o.Rid,
                o.OrderId,
                o.Order.MarketplaceItems
                    .Where(i => i.MarketplaceCard != null)
                    .Select(i => new ItemRow(i.Id, i.CatalogItemId, i.MarketplaceCard!.Sku))
                    .ToList()))
            .ToListAsync(ct);

        return new UnitIndex(
            postings.ToDictionary(p => p.PostingNumber),
            postings.Where(p => p.ExternalOrderNumber != null).ToLookup(p => p.ExternalOrderNumber!),
            // WB gives each task its own rid; DistinctBy only keeps a duplicate from failing the whole page
            postings.Where(p => p.Rid != null).DistinctBy(p => p.Rid).ToDictionary(p => p.Rid!));
    }

    /// <summary>
    /// A rid names its posting exactly and wins. A unit naming a posting links to it. A unit naming an order — Ozon
    /// files some item fees that way, Wildberries a row of a posting it left without its number — links to the
    /// posting of that order holding the SKU, or to its only posting; a shop-level unit never does. A row with a rid
    /// takes only a posting whose rid is not known yet: one with another rid is not it. The line is found by SKU;
    /// a row about no product of the posting stays on the order alone.
    /// </summary>
    private static bool TryLink(MarketplaceAccrual target, UnitIndex index)
    {
        if (target.OrderId is not null || !LinkableScopes.Contains(target.Scope))
            return false;

        var posting = target.Rid is { } rid ? index.ByRid.GetValueOrDefault(rid) : null;
        if (posting is null && target.UnitNumber is { } number)
            posting = index.ByPosting.GetValueOrDefault(number);
        if (posting is null && target.UnitNumber is { } unit && OrderNumberScopes.Contains(target.Scope))
        {
            var candidates = index.ByOrder[unit].Where(p => target.Rid is null || p.Rid is null).ToList();
            posting = candidates.FirstOrDefault(p => target.Sku is not null && p.Items.Any(i => i.Sku == target.Sku))
                      ?? (candidates.Count == 1 ? candidates[0] : null);
        }

        if (posting is null)
            return false;

        var line = target.Sku is { } sku ? posting.Items.FirstOrDefault(i => i.Sku == sku) : null;

        target.OrderId = posting.OrderId;
        target.OrderMarketplaceItemId = line?.Id;
        target.CatalogItemId = line?.CatalogItemId;
        return true;
    }

    private sealed record UnitIndex(
        Dictionary<string, PostingRow> ByPosting,
        ILookup<string, PostingRow> ByOrder,
        Dictionary<string, PostingRow> ByRid)
    {
        public static readonly UnitIndex Empty =
            new([], Array.Empty<PostingRow>().ToLookup(p => p.PostingNumber), []);
    }

    private sealed record PostingRow(
        string PostingNumber,
        string? ExternalOrderNumber,
        string? Rid,
        Guid OrderId,
        List<ItemRow> Items);

    private sealed record ItemRow(Guid Id, Guid? CatalogItemId, string? Sku);
}
