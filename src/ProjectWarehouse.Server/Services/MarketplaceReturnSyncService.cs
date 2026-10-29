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

public class MarketplaceReturnSyncService(
    ApplicationDbContext db,
    IRealtimeNotifier realtime,
    IOptions<MarketplacesOptions> options) : IMarketplaceReturnSyncService
{
    private const int RelinkBatchSize = 500;

    private readonly OzonOptions _ozon = options.Value.Ozon;

    public async Task SyncReturnsAsync(IMarketplaceProvider provider, MarketplaceCredentials credentials,
        MarketplaceAccount account, MarketplaceSyncRun run, CancellationToken ct)
    {
        if (!provider.Capabilities.HasFlag(MarketplaceCapabilities.Returns))
            return;

        using var activity = AppTelemetry.Source.StartActivity("marketplace.sync.returns");

        var to = DateTime.UtcNow;
        var since = account.ReturnsSyncedAt is { } syncedAt
            ? syncedAt.AddHours(-_ozon.ReturnsImportOverlapHours)
            : to.AddDays(-_ozon.ReturnsImportWindowPastDays);

        await ImportAsync(provider, credentials, account, run,
            ExternalReturnQuery.ByPeriod(ExternalReturnDateKind.StatusChanged, since, to), ct);

        // only once the whole period is through, for the same reason as FboPostingsSyncedAt
        account.ReturnsSyncedAt = to;
        await db.SaveChangesAsync(ct);

        // a compensation change does not move the status-change moment, so the stream never re-reads it
        foreach (var status in Enum.GetValues<MarketplaceReturnCompensationStatus>())
            await ImportAsync(provider, credentials, account, run, ExternalReturnQuery.ByCompensation(status), ct);

        await RelinkAsync(account.Id, run, ct);

        activity?.SetTag("marketplace.returns.created", run.ReturnsCreated);
        activity?.SetTag("marketplace.returns.updated", run.ReturnsUpdated);
    }

    public async Task SyncReturnsBackfillAsync(IMarketplaceProvider provider, MarketplaceCredentials credentials,
        MarketplaceAccount account, MarketplaceSyncRun run, DateTime since, DateTime to, CancellationToken ct)
    {
        if (!provider.Capabilities.HasFlag(MarketplaceCapabilities.Returns))
            return;

        using var activity = AppTelemetry.Source.StartActivity("marketplace.sync.returns_backfill");

        await ImportAsync(provider, credentials, account, run,
            ExternalReturnQuery.ByPeriod(ExternalReturnDateKind.Returned, since, to), ct);
        await RelinkAsync(account.Id, run, ct);

        activity?.SetTag("marketplace.returns.created", run.ReturnsCreated);
    }

    private async Task ImportAsync(IMarketplaceProvider provider, MarketplaceCredentials credentials,
        MarketplaceAccount account, MarketplaceSyncRun run, ExternalReturnQuery query, CancellationToken ct)
    {
        await foreach (var fetched in provider.FetchReturnsAsync(credentials, query, ct))
        {
            run.ReturnsProcessed += fetched.Count;

            var ids = fetched.Select(r => r.ExternalId).Distinct().ToList();
            var known = await db.MarketplaceReturns
                .Where(r => r.MarketplaceAccountId == account.Id && ids.Contains(r.ExternalId))
                .ToDictionaryAsync(r => r.ExternalId, ct);

            // a linked return keeps its posting, so only the unlinked ones are looked up
            bool NeedsPosting(ExternalReturn r) =>
                r.PostingNumber.Length == 0 && r.ExemplarId is not null
                && !(known.TryGetValue(r.ExternalId, out var existing) && existing.OrderId is not null);

            var postings = await ResolvePostingsAsync(account.Id,
                fetched.Where(NeedsPosting).Select(r => r.ExemplarId!).ToHashSet(), ct);
            var page = fetched
                .Select(r => NeedsPosting(r) && postings.TryGetValue(r.ExemplarId!, out var posting)
                    ? r with { PostingNumber = posting.PostingNumber, Scheme = posting.Scheme }
                    : r)
                .ToList();

            var unlinked = page
                .Where(r => !known.TryGetValue(r.ExternalId, out var existing) || existing.OrderId is null)
                .Select(r => r.PostingNumber)
                .ToHashSet();
            var orders = await LoadOrdersAsync(account.Id, unlinked, ct);

            foreach (var external in page)
            {
                if (known.TryGetValue(external.ExternalId, out var existing))
                {
                    var changed = Apply(existing, external);
                    changed |= TryLink(existing, orders);
                    if (changed)
                        run.ReturnsUpdated++;
                    continue;
                }

                var created = new MarketplaceReturn
                {
                    Id = Guid.NewGuid(),
                    MarketplaceAccountId = account.Id,
                    ExternalId = external.ExternalId,
                };
                Apply(created, external);
                TryLink(created, orders);

                db.MarketplaceReturns.Add(created);
                // a return listed twice in one page must update on its second sighting, not insert again
                known[external.ExternalId] = created;
                run.ReturnsCreated++;
            }

            await db.SaveChangesAsync(ct);
            await realtime.PublishProgressAsync(run, ct);
        }
    }

    /// <summary>
    /// Returns saved before their posting was imported — the FBO import lags by the account interval, and
    /// history may reach back further than the orders do. Only rows whose posting is known now are loaded, and
    /// those whose report named no posting, which is looked up by their exemplar.
    /// </summary>
    private async Task RelinkAsync(Guid accountId, MarketplaceSyncRun run, CancellationToken ct)
    {
        var pending = await db.MarketplaceReturns
            .Where(r => r.MarketplaceAccountId == accountId
                        && r.OrderId == null
                        && (db.MarketplaceOrders.Any(o =>
                                o.MarketplaceAccountId == accountId && o.PostingNumber == r.PostingNumber)
                            || (r.PostingNumber == "" && r.ExemplarId != null
                                && (db.MarketplaceOrders.Any(o =>
                                        o.MarketplaceAccountId == accountId && o.LabelBarcode == r.ExemplarId)
                                    || db.MarketplaceAccruals.Any(a =>
                                        a.MarketplaceAccountId == accountId && a.ExemplarId == r.ExemplarId
                                        && a.Order!.MarketplaceOrder != null)))))
            .ToListAsync(ct);

        foreach (var batch in pending.Chunk(RelinkBatchSize))
        {
            var postings = await ResolvePostingsAsync(accountId,
                batch.Where(r => r.PostingNumber.Length == 0 && r.ExemplarId is not null)
                    .Select(r => r.ExemplarId!).ToHashSet(), ct);
            foreach (var marketplaceReturn in batch)
                if (marketplaceReturn is { PostingNumber.Length: 0, ExemplarId: { } exemplar }
                    && postings.TryGetValue(exemplar, out var posting))
                {
                    marketplaceReturn.PostingNumber = posting.PostingNumber;
                    marketplaceReturn.Scheme = posting.Scheme;
                }

            var orders = await LoadOrdersAsync(accountId, batch.Select(r => r.PostingNumber).ToHashSet(), ct);

            foreach (var marketplaceReturn in batch)
                if (TryLink(marketplaceReturn, orders))
                    run.ReturnsUpdated++;

            await db.SaveChangesAsync(ct);
        }
    }

    private async Task<Dictionary<string, OrderRow>> LoadOrdersAsync(
        Guid accountId, IReadOnlyCollection<string> postingNumbers, CancellationToken ct)
    {
        if (postingNumbers.Count == 0)
            return [];

        return await db.MarketplaceOrders
            .Where(o => o.MarketplaceAccountId == accountId && postingNumbers.Contains(o.PostingNumber))
            .Select(o => new OrderRow(
                o.PostingNumber,
                o.OrderId,
                o.Order.MarketplaceItems
                    .Where(i => i.MarketplaceCard != null)
                    .Select(i => new ItemRow(i.Id, i.CatalogItemId, i.MarketplaceCard!.Sku, i.MarketplaceCard.OfferId))
                    .ToList()))
            .ToDictionaryAsync(o => o.PostingNumber, ct);
    }

    /// <summary>
    /// The posting of a returned exemplar the report could not name: the task's own sticker first, then a journal
    /// row of the same exemplar already linked to its posting. A source naming several postings is not trusted.
    /// </summary>
    private async Task<Dictionary<string, (string PostingNumber, MarketplaceReturnScheme Scheme)>>
        ResolvePostingsAsync(Guid accountId, IReadOnlyCollection<string> exemplars, CancellationToken ct)
    {
        if (exemplars.Count == 0)
            return [];

        var byLabel = await db.MarketplaceOrders
            .Where(o => o.MarketplaceAccountId == accountId && exemplars.Contains(o.LabelBarcode!))
            .Select(o => new { Exemplar = o.LabelBarcode!, o.PostingNumber, o.Order!.Type })
            .ToListAsync(ct);
        var byJournal = await db.MarketplaceAccruals
            .Where(a => a.MarketplaceAccountId == accountId
                        && exemplars.Contains(a.ExemplarId!)
                        && a.Order!.MarketplaceOrder != null)
            .Select(a => new { Exemplar = a.ExemplarId!, a.Order!.MarketplaceOrder!.PostingNumber, a.Order.Type })
            .Distinct()
            .ToListAsync(ct);

        var result = new Dictionary<string, (string, MarketplaceReturnScheme)>();
        foreach (var source in new[] { byLabel, byJournal })
            foreach (var group in source.GroupBy(x => x.Exemplar))
                if (!result.ContainsKey(group.Key)
                    && group.DistinctBy(x => x.PostingNumber).ToList() is [var posting])
                    result[group.Key] = (posting.PostingNumber, SchemeOf(posting.Type));
        return result;
    }

    internal static MarketplaceReturnScheme SchemeOf(OrderType type) =>
        type == OrderType.FboPosting ? MarketplaceReturnScheme.Fbo : MarketplaceReturnScheme.Fbs;

    /// <summary>
    /// Once linked, never reconsidered. The line is found through its card, SKU first and seller article second
    /// — the order import's own precedence — and several returned units of one product share a line.
    /// </summary>
    private static bool TryLink(MarketplaceReturn target, IReadOnlyDictionary<string, OrderRow> orders)
    {
        if (target.OrderId is not null || !orders.TryGetValue(target.PostingNumber, out var order))
            return false;

        var line = (target.Sku is { Length: > 0 } sku ? order.Items.FirstOrDefault(i => i.Sku == sku) : null)
                   ?? (target.OfferId.Length > 0 ? order.Items.FirstOrDefault(i => i.OfferId == target.OfferId) : null);

        target.OrderId = order.OrderId;
        target.OrderMarketplaceItemId = line?.Id;
        target.CatalogItemId = line?.CatalogItemId;
        return true;
    }

    /// <summary>
    /// Overwrites every marketplace-owned field; true when any of them actually changed. A return the journal
    /// shows as a reversed sale stays a counted customer return and keeps the journal's values the report lacks,
    /// and a linked one keeps its posting and product when the report cannot name them.
    /// </summary>
    private static bool Apply(MarketplaceReturn target, ExternalReturn source)
    {
        var keepPosting = source.PostingNumber.Length == 0 && target.OrderId is not null;
        var postingNumber = keepPosting ? target.PostingNumber : source.PostingNumber;
        var scheme = keepPosting ? target.Scheme : source.Scheme;
        var reversed = target.SaleReversedOn is not null;
        var kind = reversed ? MarketplaceReturnKind.CustomerReturn : source.Kind;
        var isCancelled = !reversed && source.IsCancelled;
        // without the task the report's product is the card's today, not the one sold
        var keepProduct = reversed || target.OrderId is not null;
        var sku = keepPosting ? target.Sku ?? source.Sku
            : keepProduct ? source.Sku ?? target.Sku
            : source.Sku;
        var offerId = keepPosting && target.OfferId.Length > 0 ? target.OfferId
            : keepProduct && source.OfferId.Length == 0 ? target.OfferId
            : source.OfferId;
        var rawKind = reversed ? source.RawKind ?? target.RawKind : source.RawKind;
        var price = reversed ? source.Price ?? target.Price : source.Price;
        var currencyCode = reversed ? source.CurrencyCode ?? target.CurrencyCode : source.CurrencyCode;
        var reasonCategory = reversed ? source.ReasonCategory ?? target.ReasonCategory : source.ReasonCategory;

        var changed =
            target.PostingNumber != postingNumber
            || target.Sku != sku
            || target.OfferId != offerId
            || target.Scheme != scheme
            || target.RawScheme != source.RawScheme
            || target.Kind != kind
            || target.RawKind != rawKind
            || target.Quantity != source.Quantity
            || target.Price != price
            || target.CurrencyCode != currencyCode
            || target.Reason != source.Reason
            || target.ReasonCategory != reasonCategory
            || target.RawReasonCode != source.RawReasonCode
            || target.RawStatus != source.RawStatus
            || target.StatusName != source.StatusName
            || target.IsCancelled != isCancelled
            || target.StatusChangedAt != source.StatusChangedAt
            || target.ReturnedAt != source.ReturnedAt
            || target.FinalAt != source.FinalAt
            || target.CompensationStatus != source.CompensationStatus
            || target.CompensationStatusChangedAt != source.CompensationStatusChangedAt
            || target.SourceExternalId != source.SourceExternalId
            || target.ExemplarId != source.ExemplarId;

        target.PostingNumber = postingNumber;
        target.Sku = sku;
        target.OfferId = offerId;
        target.Scheme = scheme;
        target.RawScheme = source.RawScheme;
        target.Kind = kind;
        target.RawKind = rawKind;
        target.Quantity = source.Quantity;
        target.Price = price;
        target.CurrencyCode = currencyCode;
        target.Reason = source.Reason;
        target.ReasonCategory = reasonCategory;
        target.RawReasonCode = source.RawReasonCode;
        target.RawStatus = source.RawStatus;
        target.StatusName = source.StatusName;
        target.IsCancelled = isCancelled;
        target.StatusChangedAt = source.StatusChangedAt;
        target.ReturnedAt = source.ReturnedAt;
        target.FinalAt = source.FinalAt;
        target.CompensationStatus = source.CompensationStatus;
        target.CompensationStatusChangedAt = source.CompensationStatusChangedAt;
        target.SourceExternalId = source.SourceExternalId;
        target.ExemplarId = source.ExemplarId;
        target.SyncedAt = DateTime.UtcNow;

        return changed;
    }

    private sealed record OrderRow(string PostingNumber, Guid OrderId, List<ItemRow> Items);

    private sealed record ItemRow(Guid Id, Guid? CatalogItemId, string? Sku, string OfferId);
}
