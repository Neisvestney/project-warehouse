using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ProjectWarehouse.Server.Data;
using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Infrastructure.Marketplaces;
using ProjectWarehouse.Server.Infrastructure.Observability;
using ProjectWarehouse.Server.Infrastructure.Realtime;
using ProjectWarehouse.Server.Integrations.Abstractions;
using ProjectWarehouse.Server.Integrations.Sync;

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

        // buyouts come in month-long windows, so a full pass of a shop without any yet reads its whole journal
        // in a dozen calls; a shop that never has buyouts pays that once a day
        var buyoutsFrom = from;
        if (fullPass && !await db.MarketplaceAccruals.AnyAsync(a => a.MarketplaceAccountId == account.Id
                && a.Source == MarketplaceAccrualSource.BuyoutReport, ct))
        {
            var journalStart = await db.MarketplaceAccruals
                .Where(a => a.MarketplaceAccountId == account.Id && a.Source == MarketplaceAccrualSource.AccrualJournal)
                .MinAsync(a => (DateOnly?)a.Date, ct);
            if (journalStart < buyoutsFrom)
                buyoutsFrom = journalStart.Value;
        }

        // a day ahead of UTC: the marketplace's day may already have started, the provider cuts off its future
        await ImportAsync(provider.FetchAccrualsAsync(credentials, from, today.AddDays(1), ct), account, run, ct);
        await ImportBuyoutsAsync(provider, credentials, account, run, buyoutsFrom, today.AddDays(1), ct);

        // only once the whole period is through, for the same reason as FboPostingsSyncedAt
        account.AccrualsSyncedAt = now;
        if (fullPass)
            account.AccrualsFullPassAt = now;
        await db.SaveChangesAsync(ct);

        await RelinkAsync(account.Id, run, ct);

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
        await ImportBuyoutsAsync(provider, credentials, account, run, from, till, ct);
        await RelinkAsync(account.Id, run, ct);

        activity?.SetTag("marketplace.accruals.created", run.AccrualsCreated);
    }

    private async Task ImportBuyoutsAsync(IMarketplaceProvider provider, MarketplaceCredentials credentials,
        MarketplaceAccount account, MarketplaceSyncRun run, DateOnly from, DateOnly to, CancellationToken ct)
    {
        if (provider.Capabilities.HasFlag(MarketplaceCapabilities.Buyouts))
            await ImportAsync(provider.FetchBuyoutsAsync(credentials, from, to, ct), account, run, ct);
    }

    private async Task ImportAsync(IAsyncEnumerable<IReadOnlyList<ExternalAccrual>> pages,
        MarketplaceAccount account, MarketplaceSyncRun run, CancellationToken ct)
    {
        await foreach (var fetched in pages)
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
            var orders = await LoadOrdersAsync(account.Id,
                changed.Where(a => LinkableScopes.Contains(a.Scope))
                    .Select(a => a.UnitNumber)
                    .OfType<string>()
                    .ToHashSet(),
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
            && p.First.Category == p.Second.Category
            && p.First.RawTypeId == p.Second.RawTypeId
            && p.First.Sku == p.Second.Sku
            && p.First.Amount == p.Second.Amount
            && p.First.CurrencyCode == p.Second.CurrencyCode);

    /// <summary>
    /// The buyout report dates no row. A buyout takes the Moscow day its posting was seen delivered, else the day
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
    /// Accruals saved before their posting was imported, which the order sync catches up with on its own
    /// interval. Only rows whose unit is known as a posting or an order now are loaded.
    /// </summary>
    private async Task RelinkAsync(Guid accountId, MarketplaceSyncRun run, CancellationToken ct)
    {
        var pending = await db.MarketplaceAccruals
            .Where(a => a.MarketplaceAccountId == accountId
                        && a.OrderId == null
                        && a.UnitNumber != null
                        && LinkableScopes.Contains(a.Scope)
                        && db.MarketplaceOrders.Any(o => o.MarketplaceAccountId == accountId
                                                         && (o.PostingNumber == a.UnitNumber
                                                             || (OrderNumberScopes.Contains(a.Scope)
                                                                 && o.ExternalOrderNumber == a.UnitNumber))))
            .ToListAsync(ct);

        foreach (var batch in pending.Chunk(RelinkBatchSize))
        {
            var orders = await LoadOrdersAsync(accountId, batch.Select(a => a.UnitNumber!).ToHashSet(), ct);

            var linked = batch.Where(a => TryLink(a, orders)).Select(a => a.ExternalId).Distinct().Count();
            run.AccrualsUpdated += linked;

            await db.SaveChangesAsync(ct);
        }
    }

    /// <summary>Postings named by a unit number either directly or through the order they belong to.</summary>
    private async Task<UnitIndex> LoadOrdersAsync(Guid accountId, IReadOnlyCollection<string> units,
        CancellationToken ct)
    {
        if (units.Count == 0)
            return UnitIndex.Empty;

        var postings = await db.MarketplaceOrders
            .Where(o => o.MarketplaceAccountId == accountId
                        && (units.Contains(o.PostingNumber)
                            || (o.ExternalOrderNumber != null && units.Contains(o.ExternalOrderNumber))))
            .OrderBy(o => o.PostingNumber)
            .Select(o => new PostingRow(
                o.PostingNumber,
                o.ExternalOrderNumber,
                o.OrderId,
                o.Order.MarketplaceItems
                    .Where(i => i.MarketplaceCard != null)
                    .Select(i => new ItemRow(i.Id, i.CatalogItemId, i.MarketplaceCard!.Sku))
                    .ToList()))
            .ToListAsync(ct);

        return new UnitIndex(
            postings.ToDictionary(p => p.PostingNumber),
            postings.Where(p => p.ExternalOrderNumber != null).ToLookup(p => p.ExternalOrderNumber!));
    }

    /// <summary>
    /// A unit naming a posting links to it. A unit naming an order — Ozon files some item fees that way — links
    /// to the posting of that order holding the SKU, or to its only posting; a shop-level unit never does. The
    /// line is found by SKU; a row about no product of the posting stays on the order alone.
    /// </summary>
    private static bool TryLink(MarketplaceAccrual target, UnitIndex index)
    {
        if (target.OrderId is not null || target.UnitNumber is not { } unit || !LinkableScopes.Contains(target.Scope))
            return false;

        var posting = index.ByPosting.GetValueOrDefault(unit);
        if (posting is null && OrderNumberScopes.Contains(target.Scope))
        {
            var candidates = index.ByOrder[unit].ToList();
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
        ILookup<string, PostingRow> ByOrder)
    {
        public static readonly UnitIndex Empty =
            new([], Array.Empty<PostingRow>().ToLookup(p => p.PostingNumber));
    }

    private sealed record PostingRow(
        string PostingNumber,
        string? ExternalOrderNumber,
        Guid OrderId,
        List<ItemRow> Items);

    private sealed record ItemRow(Guid Id, Guid? CatalogItemId, string? Sku);
}
