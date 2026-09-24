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
        await foreach (var page in provider.FetchReturnsAsync(credentials, query, ct))
        {
            run.ReturnsProcessed += page.Count;

            var ids = page.Select(r => r.ExternalId).Distinct().ToList();
            var known = await db.MarketplaceReturns
                .Where(r => r.MarketplaceAccountId == account.Id && ids.Contains(r.ExternalId))
                .ToDictionaryAsync(r => r.ExternalId, ct);

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
    /// history may reach back further than the orders do. Only rows whose posting is known now are loaded.
    /// </summary>
    private async Task RelinkAsync(Guid accountId, MarketplaceSyncRun run, CancellationToken ct)
    {
        var pending = await db.MarketplaceReturns
            .Where(r => r.MarketplaceAccountId == accountId
                        && r.OrderId == null
                        && db.MarketplaceOrders.Any(o =>
                            o.MarketplaceAccountId == accountId && o.PostingNumber == r.PostingNumber))
            .ToListAsync(ct);

        foreach (var batch in pending.Chunk(RelinkBatchSize))
        {
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

    /// <summary>Overwrites every marketplace-owned field; true when any of them actually changed.</summary>
    private static bool Apply(MarketplaceReturn target, ExternalReturn source)
    {
        var changed =
            target.PostingNumber != source.PostingNumber
            || target.Sku != source.Sku
            || target.OfferId != source.OfferId
            || target.Scheme != source.Scheme
            || target.RawScheme != source.RawScheme
            || target.Kind != source.Kind
            || target.RawKind != source.RawKind
            || target.Quantity != source.Quantity
            || target.Price != source.Price
            || target.CurrencyCode != source.CurrencyCode
            || target.Reason != source.Reason
            || target.RawStatus != source.RawStatus
            || target.StatusName != source.StatusName
            || target.IsCancelled != source.IsCancelled
            || target.StatusChangedAt != source.StatusChangedAt
            || target.ReturnedAt != source.ReturnedAt
            || target.FinalAt != source.FinalAt
            || target.CompensationStatus != source.CompensationStatus
            || target.CompensationStatusChangedAt != source.CompensationStatusChangedAt
            || target.SourceExternalId != source.SourceExternalId
            || target.ExemplarId != source.ExemplarId;

        target.PostingNumber = source.PostingNumber;
        target.Sku = source.Sku;
        target.OfferId = source.OfferId;
        target.Scheme = source.Scheme;
        target.RawScheme = source.RawScheme;
        target.Kind = source.Kind;
        target.RawKind = source.RawKind;
        target.Quantity = source.Quantity;
        target.Price = source.Price;
        target.CurrencyCode = source.CurrencyCode;
        target.Reason = source.Reason;
        target.RawStatus = source.RawStatus;
        target.StatusName = source.StatusName;
        target.IsCancelled = source.IsCancelled;
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
