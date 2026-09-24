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

public class MarketplaceOrderSyncService(
    ApplicationDbContext db,
    IRealtimeNotifier realtime,
    IMarketplaceReturnSyncService returnSync,
    IOptions<MarketplacesOptions> options,
    ILogger<MarketplaceOrderSyncService> logger) : IMarketplaceOrderSyncService
{
    /// <summary>
    /// Above this the list stops growing; OrdersSkipped keeps counting. A store with an unmapped
    /// catalog would otherwise inflate a single jsonb row into megabytes the UI never shows.
    /// </summary>
    private const int SkippedCap = 100;

    /// <summary>
    /// What the history import asks for: everything the marketplace will not move backwards from, plus the
    /// in-transit state, which is as close to done as an old posting gets. Anything earlier is left to the
    /// ordinary import, which knows how to assemble it.
    /// </summary>
    private static readonly MarketplaceOrderStatus[] BackfillStatuses =
        [MarketplaceOrderStatus.Delivering, MarketplaceOrderStatus.Delivered, MarketplaceOrderStatus.Cancelled];

    private readonly OzonOptions _ozon = options.Value.Ozon;

    public async Task SyncOrdersAsync(IMarketplaceProvider provider, MarketplaceCredentials credentials,
        MarketplaceAccount account, MarketplaceSyncRun run, CancellationToken ct)
    {
        using var activity = AppTelemetry.Source.StartActivity("marketplace.sync.orders");

        var skipped = new List<SkippedOrderInfo>();

        await DiscoverPostingsAsync(provider, credentials, account, run, skipped, ct);
        await SyncOrdersBackgroundAsync(provider, credentials, account, run, ct);

        // A fresh instance, not an in-place mutation: a jsonb scalar has no value comparer, so change
        // tracking cannot see the list grow — the same reason cards do `Barcodes = [.. …]`.
        run.SkippedOrders = skipped.Count > 0 ? [.. skipped] : null;
        await db.SaveChangesAsync(ct);

        activity?.SetTag("marketplace.orders.created", run.OrdersCreated);
        activity?.SetTag("marketplace.orders.updated", run.OrdersUpdated);
        activity?.SetTag("marketplace.orders.skipped", skipped.Count);
    }

    // ── Phase 1: discovery ────────────────────────────────────────────────────

    private async Task DiscoverPostingsAsync(IMarketplaceProvider provider, MarketplaceCredentials credentials,
        MarketplaceAccount account, MarketplaceSyncRun run, List<SkippedOrderInfo> skipped, CancellationToken ct)
    {
        var warehouses = await LoadWarehousesAsync(account.Id, ct);

        await foreach (var page in provider.FetchActivePostingsAsync(credentials, ct))
        {
            run.OrdersProcessed += page.Count;

            var known = await LoadKnownAsync(account.Id, page, ct);
            var cards = await LoadCardsAsync(account.Id, page, ct);

            foreach (var posting in page)
            {
                if (known.TryGetValue(posting.PostingNumber, out var existing))
                {
                    if (ApplyPosting(existing, posting))
                        run.OrdersUpdated++;
                    continue;
                }

                if (TryBuildOrder(posting, account, warehouses, cards, out var order, out var skip))
                {
                    db.Orders.Add(order!);
                    run.OrdersCreated++;
                }
                else
                {
                    run.OrdersSkipped++;
                    if (skipped.Count < SkippedCap)
                        skipped.Add(skip!);
                }
            }

            // per page, so the sync modal's counters advance while the run is still going
            await db.SaveChangesAsync(ct);
            await realtime.PublishProgressAsync(run, ct);
        }
    }

    // warehouses are a handful per seller, so they are loaded once; cards are not, and go per page
    private async Task<Dictionary<string, Guid>> LoadWarehousesAsync(Guid accountId, CancellationToken ct) =>
        await db.MarketplaceWarehouses
            .Where(w => w.MarketplaceAccountId == accountId && w.WarehouseId != null && !w.IsArchived)
            .ToDictionaryAsync(w => w.ExternalId, w => w.WarehouseId!.Value, ct);

    private async Task<Dictionary<string, MarketplaceOrder>> LoadKnownAsync(
        Guid accountId, IReadOnlyList<ExternalPosting> page, CancellationToken ct)
    {
        var numbers = page.Select(p => p.PostingNumber).ToList();

        return await db.MarketplaceOrders
            .Where(o => o.MarketplaceAccountId == accountId && numbers.Contains(o.PostingNumber))
            .Include(o => o.Order)
            .ThenInclude(o => o!.MarketplaceItems)
            .ThenInclude(i => i.MarketplaceCard)
            .AsSplitQuery()
            .ToDictionaryAsync(o => o.PostingNumber, ct);
    }

    /// <summary>
    /// Postings carry <c>sku</c> and <c>offer_id</c> but never <c>product_id</c>, so cards are looked up
    /// by SKU first and by seller article second.
    /// </summary>
    private async Task<CardLookup> LoadCardsAsync(
        Guid accountId, IReadOnlyList<ExternalPosting> page, CancellationToken ct)
    {
        var skus = page.SelectMany(p => p.Items).Select(i => i.Sku).OfType<string>().Distinct().ToList();
        var offerIds = page.SelectMany(p => p.Items)
            .Select(i => i.OfferId).Where(o => o.Length > 0).Distinct().ToList();

        var rows = await db.MarketplaceCards
            .Where(c => c.MarketplaceAccountId == accountId
                        && ((c.Sku != null && skus.Contains(c.Sku)) || offerIds.Contains(c.OfferId)))
            .Select(c => new CardRow(c.Id, c.Sku, c.OfferId, c.CatalogItemId))
            .ToListAsync(ct);

        var lookup = new CardLookup();

        foreach (var row in rows)
        {
            if (row.Sku is { Length: > 0 })
                lookup.BySku.TryAdd(row.Sku, row);

            // offer_id is unique per Ozon account in practice, but nothing in the schema enforces it
            if (row.OfferId.Length > 0 && !lookup.ByOfferId.TryAdd(row.OfferId, row))
                logger.LogWarning("Account {AccountId} has more than one card with offer_id {OfferId}",
                    accountId, row.OfferId);
        }

        return lookup;
    }

    private bool TryBuildOrder(ExternalPosting posting, MarketplaceAccount account,
        IReadOnlyDictionary<string, Guid> warehouses, CardLookup cards,
        out Order? order, out SkippedOrderInfo? skip)
    {
        order = null;

        if (posting.WarehouseExternalId is null || !warehouses.TryGetValue(posting.WarehouseExternalId, out var warehouseId))
        {
            skip = new SkippedOrderInfo
            {
                PostingNumber = posting.PostingNumber,
                Reason = ErrorCode.MarketplaceOrderWarehouseNotMapped,
            };
            return false;
        }

        var lines = new List<OrderLine>(posting.Items.Count);
        var unmapped = new List<string>();

        foreach (var item in posting.Items)
        {
            var card = cards.Find(item);
            if (card is null || card.CatalogItemId is null)
                unmapped.Add(item.OfferId);
            else
                lines.Add(new OrderLine(card.Id, card.CatalogItemId, item));
        }

        if (unmapped.Count > 0)
        {
            skip = new SkippedOrderInfo
            {
                PostingNumber = posting.PostingNumber,
                Reason = ErrorCode.MarketplaceOrderCardNotMapped,
                OfferIds = [.. unmapped.Distinct()],
            };
            return false;
        }

        order = BuildOrder(posting, account, warehouseId, lines, isExternal: false);
        skip = null;
        return true;
    }

    /// <summary>
    /// The shared shape of an imported posting. An external one differs only in where it starts: already
    /// shipped, with box components for whatever positions the catalog recognized and nothing for the rest.
    /// </summary>
    private static Order BuildOrder(ExternalPosting posting, MarketplaceAccount account,
        Guid? warehouseId, IReadOnlyList<OrderLine> lines, bool isExternal)
    {
        var now = DateTime.UtcNow;
        var orderId = Guid.NewGuid();
        var boxId = Guid.NewGuid();

        return new Order
        {
            Id = orderId,
            Type = posting.Scheme == ExternalPostingScheme.Fbo ? OrderType.FboPosting : OrderType.FBS,
            // the posting arrives already packed on the marketplace side, so it is ready to assemble
            Status = isExternal ? OrderStatus.Shipped : OrderStatus.Confirmed,
            IsExternal = isExternal,
            WarehouseId = warehouseId,
            PlannedShipmentAt = posting.ShipmentDate,
            CreatedAt = now,
            // Shipped is where an external order starts, so it needs a date even for a cancelled posting;
            // the marketplace state it really is in lives on MarketplaceOrder.Status.
            ShippedAt = isExternal ? posting.InProcessAt ?? posting.CreatedAt ?? now : null,
            // created by the integration; who started the run is recorded on MarketplaceSyncRun
            CreatedById = null,
            MarketplaceItems = [.. lines.Select(l => new OrderMarketplaceItem
            {
                Id = Guid.NewGuid(),
                OrderId = orderId,
                MarketplaceCardId = l.CardId,
                CatalogItemId = l.CatalogItemId,
                Quantity = l.Item.Quantity,
                CustomerPrice = l.Item.CustomerPrice,
                CustomerCurrencyCode = l.Item.CustomerCurrencyCode,
                Price = l.Item.Price,
                OldPrice = l.Item.OldPrice,
                DiscountValue = l.Item.DiscountValue,
                Payout = l.Item.Payout,
                CurrencyCode = l.Item.CurrencyCode,
                CommissionAmount = l.Item.CommissionAmount,
                CommissionCurrencyCode = l.Item.CommissionCurrencyCode,
            })],
            Boxes =
            [
                new OrderBox
                {
                    Id = boxId,
                    OrderId = orderId,
                    // Always one box, even when MultiBoxQty > 1: the marketplace says how many packages
                    // but not what goes in which, so the packer splits them during assembly.
                    Components = [.. lines
                        .Where(l => l.CatalogItemId is not null)
                        .GroupBy(l => l.CatalogItemId!.Value)
                        .Select(g => new OrderBoxComponent
                        {
                            Id = Guid.NewGuid(),
                            OrderBoxId = boxId,
                            CatalogItemId = g.Key,
                            Quantity = g.Sum(l => l.Item.Quantity),
                        })],
                },
            ],
            MarketplaceOrder = new MarketplaceOrder
            {
                OrderId = orderId,
                MarketplaceAccountId = account.Id,
                PostingNumber = posting.PostingNumber,
                ExternalOrderNumber = posting.ExternalOrderNumber,
                Status = posting.Status,
                RawStatus = posting.RawStatus,
                RawSubstatus = posting.RawSubstatus,
                CancelledAfterShip = posting.Cancellation?.CancelledAfterShip,
                CancellationType = posting.Cancellation?.Type,
                RawCancellationType = posting.Cancellation?.RawType,
                CancelReason = posting.Cancellation?.Reason,
                ShipmentDate = posting.ShipmentDate,
                InProcessAt = posting.InProcessAt,
                TrackingNumber = posting.TrackingNumber,
                DeliveryMethodName = posting.DeliveryMethodName,
                MultiBoxQty = posting.MultiBoxQty,
                ScanitBarcode = posting.Scanit,
                StatusSyncedAt = now,
                SyncedAt = now,
            },
        };
    }

    /// <summary>
    /// The idempotency contract: marketplace-owned fields only. Boxes, components, assembly tasks and
    /// fulfillments are never touched — by the time a posting is re-seen they may be half assembled.
    /// </summary>
    private static bool ApplyPosting(MarketplaceOrder known, ExternalPosting posting)
    {
        var changed =
            known.Status != posting.Status
            || known.RawStatus != posting.RawStatus
            || known.RawSubstatus != posting.RawSubstatus
            || known.ShipmentDate != posting.ShipmentDate
            || known.InProcessAt != posting.InProcessAt
            || known.TrackingNumber != posting.TrackingNumber
            || known.DeliveryMethodName != posting.DeliveryMethodName
            || known.MultiBoxQty != posting.MultiBoxQty
            || (posting.Scanit is not null && known.ScanitBarcode != posting.Scanit)
            || known.ExternalOrderNumber != posting.ExternalOrderNumber;

        changed |= ApplyCancellation(known, posting.Cancellation);
        changed |= ApplyItemFinancials(known.Order, posting.Items);

        if (known.ShipmentDate != posting.ShipmentDate && known.Order is not null)
            known.Order.PlannedShipmentAt = posting.ShipmentDate;

        var now = DateTime.UtcNow;
        StampFinalStatus(known, posting.Status, now);

        known.Status = posting.Status;
        known.RawStatus = posting.RawStatus;
        known.RawSubstatus = posting.RawSubstatus;
        known.ShipmentDate = posting.ShipmentDate;
        known.InProcessAt = posting.InProcessAt;
        known.TrackingNumber = posting.TrackingNumber;
        known.DeliveryMethodName = posting.DeliveryMethodName;
        known.MultiBoxQty = posting.MultiBoxQty;
        // Ozon blanks the barcode once the posting leaves awaiting_deliver; the stored one outlives that
        known.ScanitBarcode = posting.Scanit ?? known.ScanitBarcode;
        known.ExternalOrderNumber = posting.ExternalOrderNumber;

        known.StatusSyncedAt = now;
        known.SyncedAt = now;

        return changed;
    }

    // ── Phase 2: unattended refresh ───────────────────────────────────────────

    public async Task SyncOrdersBackgroundAsync(IMarketplaceProvider provider, MarketplaceCredentials credentials,
        MarketplaceAccount account, MarketplaceSyncRun run, CancellationToken ct)
    {
        using var activity = AppTelemetry.Source.StartActivity("marketplace.sync.orders_background");

        await ImportFboPostingsAsync(provider, credentials, account, run, ct);
        await CatchUpStatusesAsync(provider, credentials, account, run, ExternalPostingScheme.Fbs, ct);
        await CatchUpStatusesAsync(provider, credentials, account, run, ExternalPostingScheme.Fbo, ct);
        // last, so the postings its returns belong to are already in
        await returnSync.SyncReturnsAsync(provider, credentials, account, run, ct);
    }

    /// <summary>
    /// Marketplace-fulfilled postings never reach the unfulfilled list — the goods are already at Ozon, so
    /// there is nothing for a warehouse to do. They are asked for by period instead: from where the last
    /// import got to, minus an overlap, up to now.
    /// </summary>
    private async Task ImportFboPostingsAsync(IMarketplaceProvider provider, MarketplaceCredentials credentials,
        MarketplaceAccount account, MarketplaceSyncRun run, CancellationToken ct)
    {
        var to = DateTime.UtcNow;
        var since = account.FboPostingsSyncedAt is { } syncedAt
            ? syncedAt.AddHours(-_ozon.FboImportOverlapHours)
            : to.AddDays(-_ozon.FboImportWindowPastDays);

        await ImportExternalPostingsAsync(provider, credentials, account, run,
            new ExternalPostingQuery(ExternalPostingScheme.Fbo, since, to), refreshKnown: true, ct);

        // Only once the whole period is through: a run that threw halfway must leave the mark where it was,
        // so the next one covers the same ground again rather than skipping what it never read.
        account.FboPostingsSyncedAt = to;
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Postings that leave <c>awaiting_deliver</c> disappear from the unfulfilled list, and "shipped" is
    /// indistinguishable from "cancelled" by absence alone — so open ones are asked about directly.
    /// </summary>
    private async Task CatchUpStatusesAsync(IMarketplaceProvider provider, MarketplaceCredentials credentials,
        MarketplaceAccount account, MarketplaceSyncRun run, ExternalPostingScheme scheme, CancellationToken ct)
    {
        var isFbo = scheme == ExternalPostingScheme.Fbo;

        var open = await db.MarketplaceOrders
            .Where(o => o.MarketplaceAccountId == account.Id
                        && o.Status != MarketplaceOrderStatus.Delivered
                        && o.Status != MarketplaceOrderStatus.Cancelled
                        // the two schemes are asked about through different endpoints
                        && (o.Order!.Type == OrderType.FboPosting) == isFbo
                        // phase 1 just refreshed everything the unfulfilled list returned; re-asking would
                        // cost one single-posting call per open order, every run, for no new information
                        && o.StatusSyncedAt < run.StartedAt)
            .Include(o => o.Order)
            .ThenInclude(o => o!.MarketplaceItems)
            .ThenInclude(i => i.MarketplaceCard)
            .AsSplitQuery()
            .ToListAsync(ct);

        if (open.Count == 0)
            return;

        var statuses = (await provider.FetchPostingStatusesAsync(
                credentials, [.. open.Select(o => o.PostingNumber)], scheme, ct))
            .ToDictionary(s => s.PostingNumber);

        var now = DateTime.UtcNow;
        foreach (var order in open)
        {
            if (!statuses.TryGetValue(order.PostingNumber, out var status))
            {
                // the marketplace forgot it; stamp anyway so it is not re-polled forever
                logger.LogWarning("Posting {PostingNumber} is no longer known to the marketplace",
                    order.PostingNumber);
                order.StatusSyncedAt = now;
                continue;
            }

            var cancellationChanged = ApplyCancellation(order, status.Cancellation);
            var financialsChanged = ApplyItemFinancials(order.Order, status.Items);

            if (cancellationChanged
                || financialsChanged
                || order.Status != status.Status
                || order.RawStatus != status.RawStatus
                || order.RawSubstatus != status.RawSubstatus
                || order.TrackingNumber != status.TrackingNumber)
                run.OrdersUpdated++;

            StampFinalStatus(order, status.Status, now);
            order.Status = status.Status;
            order.RawStatus = status.RawStatus;
            order.RawSubstatus = status.RawSubstatus;
            order.TrackingNumber = status.TrackingNumber ?? order.TrackingNumber;
            order.StatusSyncedAt = now;
        }

        await db.SaveChangesAsync(ct);
    }

    // ── Phase 3: history backfill ─────────────────────────────────────────────

    public async Task SyncOrdersBackfillAsync(IMarketplaceProvider provider, MarketplaceCredentials credentials,
        MarketplaceAccount account, MarketplaceSyncRun run, CancellationToken ct)
    {
        using var activity = AppTelemetry.Source.StartActivity("marketplace.sync.orders_backfill");

        if (run.BackfillSince is not { } since || run.BackfillTo is not { } to)
            throw new ValidationException("since", ErrorCode.Required,
                "A backfill run needs both ends of its period.");

        foreach (var scheme in (ExternalPostingScheme[])[ExternalPostingScheme.Fbs, ExternalPostingScheme.Fbo])
            await ImportExternalPostingsAsync(provider, credentials, account, run,
                new ExternalPostingQuery(scheme, since, to, BackfillStatuses), refreshKnown: false, ct);

        await returnSync.SyncReturnsBackfillAsync(provider, credentials, account, run, since, to, ct);

        activity?.SetTag("marketplace.orders.created", run.OrdersCreated);
    }

    /// <summary>
    /// Creates whatever the period holds and WMS does not, as external orders: no warehouse gate, no
    /// catalog gate, and no box components for positions the catalog does not recognize. A posting WMS
    /// already knows is either refreshed or left alone — history must never overwrite a live order.
    /// </summary>
    private async Task ImportExternalPostingsAsync(IMarketplaceProvider provider, MarketplaceCredentials credentials,
        MarketplaceAccount account, MarketplaceSyncRun run, ExternalPostingQuery query, bool refreshKnown,
        CancellationToken ct)
    {
        // only consulted for FBS postings; an FBO one is fulfilled from stock that never was in a WMS warehouse
        var warehouses = await LoadWarehousesAsync(account.Id, ct);

        await foreach (var page in provider.FetchPostingsAsync(credentials, query, ct))
        {
            run.OrdersProcessed += page.Count;

            var known = await LoadKnownAsync(account.Id, page, ct);
            var cards = await LoadCardsAsync(account.Id, page, ct);

            foreach (var posting in page)
            {
                if (known.TryGetValue(posting.PostingNumber, out var existing))
                {
                    if (refreshKnown && ApplyPosting(existing, posting))
                        run.OrdersUpdated++;
                    continue;
                }

                var lines = ResolveLines(posting, account.Id, cards);
                var warehouseId = posting.Scheme == ExternalPostingScheme.Fbs
                                  && posting.WarehouseExternalId is { } externalId
                                  && warehouses.TryGetValue(externalId, out var mapped)
                    ? mapped
                    : (Guid?)null;

                db.Orders.Add(BuildOrder(posting, account, warehouseId, lines, isExternal: true));
                run.OrdersCreated++;
            }

            await db.SaveChangesAsync(ct);
            await realtime.PublishProgressAsync(run, ct);
        }
    }

    /// <summary>
    /// Every position of the posting, recognized or not. A position whose product has no card at all gets a
    /// placeholder one built from the posting — without it the line would carry no name, no article and
    /// nothing to map later.
    /// </summary>
    private List<OrderLine> ResolveLines(ExternalPosting posting, Guid accountId, CardLookup cards)
    {
        var lines = new List<OrderLine>(posting.Items.Count);

        foreach (var item in posting.Items)
        {
            var card = cards.Find(item) ?? CreatePlaceholderCard(item, accountId, cards);
            lines.Add(new OrderLine(card?.Id, card?.CatalogItemId, item));
        }

        return lines;
    }

    /// <summary>
    /// Archived on creation: the product is not in the card list the marketplace answers with, so it is not
    /// on sale. The external id is derived from the posting because a posting never carries a product id —
    /// the card sync adopts the row by SKU when the real card shows up.
    /// </summary>
    private CardRow? CreatePlaceholderCard(ExternalPostingItem item, Guid accountId, CardLookup cards)
    {
        var externalId = MarketplaceCardPlaceholder.ExternalIdFor(item.Sku, item.OfferId);
        if (externalId is null)
        {
            logger.LogWarning("A posting item of account {AccountId} has neither sku nor offer_id", accountId);
            return null;
        }

        var card = new MarketplaceCard
        {
            Id = Guid.NewGuid(),
            MarketplaceAccountId = accountId,
            ExternalId = externalId,
            Sku = item.Sku,
            OfferId = item.OfferId,
            Name = item.Name,
            IsArchived = true,
            SyncedAt = DateTime.UtcNow,
        };

        db.MarketplaceCards.Add(card);

        var row = new CardRow(card.Id, card.Sku, card.OfferId, null);
        if (row.Sku is { Length: > 0 })
            cards.BySku.TryAdd(row.Sku, row);
        if (row.OfferId.Length > 0)
            cards.ByOfferId.TryAdd(row.OfferId, row);

        return row;
    }

    /// <summary>
    /// The marketplace states neither moment, so a transition this sync observes is stamped with the time it was
    /// seen; leaving the state clears the stamp. Must run before <c>Status</c> is overwritten.
    /// </summary>
    private static void StampFinalStatus(MarketplaceOrder order, MarketplaceOrderStatus next, DateTime now)
    {
        if (next != MarketplaceOrderStatus.Delivered)
            order.DeliveredAt = null;
        else if (order.Status != MarketplaceOrderStatus.Delivered)
            order.DeliveredAt = now;

        if (next != MarketplaceOrderStatus.Cancelled)
            order.CancelledAt = null;
        else if (order.Status != MarketplaceOrderStatus.Cancelled)
            order.CancelledAt = now;
    }

    /// <summary>
    /// Mirrors the marketplace's cancellation block, clearing included: a posting that stops reporting one
    /// is no longer cancelled, and stale fields would outlive the state they describe.
    /// </summary>
    private static bool ApplyCancellation(MarketplaceOrder known, ExternalCancellation? cancellation)
    {
        var changed =
            known.CancelledAfterShip != cancellation?.CancelledAfterShip
            || known.CancellationType != cancellation?.Type
            || known.RawCancellationType != cancellation?.RawType
            || known.CancelReason != cancellation?.Reason;

        known.CancelledAfterShip = cancellation?.CancelledAfterShip;
        known.CancellationType = cancellation?.Type;
        known.RawCancellationType = cancellation?.RawType;
        known.CancelReason = cancellation?.Reason;

        return changed;
    }

    /// <summary>
    /// Refreshes money only — which card and catalog item a line points at stays as imported. A line the
    /// marketplace reports no amounts for keeps its last known ones: Ozon omits the block until it is computed.
    /// </summary>
    private static bool ApplyItemFinancials(Order? order, IReadOnlyList<ExternalPostingItem> items)
    {
        if (order is null)
            return false;

        var pending = items.Where(HasFinancials).ToList();
        var changed = false;

        foreach (var line in order.MarketplaceItems)
        {
            if (line.MarketplaceCard is not { } card)
                continue;

            // same precedence as CardLookup.Find: SKU first, seller article second
            var index = pending.FindIndex(i => i.Sku is { Length: > 0 } && i.Sku == card.Sku);
            if (index < 0)
                index = pending.FindIndex(i => i.OfferId.Length > 0 && i.OfferId == card.OfferId);
            if (index < 0)
                continue;

            var item = pending[index];
            pending.RemoveAt(index);

            changed |=
                line.CustomerPrice != item.CustomerPrice
                || line.CustomerCurrencyCode != item.CustomerCurrencyCode
                || line.Price != item.Price
                || line.OldPrice != item.OldPrice
                || line.DiscountValue != item.DiscountValue
                || line.Payout != item.Payout
                || line.CurrencyCode != item.CurrencyCode
                || line.CommissionAmount != item.CommissionAmount
                || line.CommissionCurrencyCode != item.CommissionCurrencyCode;

            line.CustomerPrice = item.CustomerPrice;
            line.CustomerCurrencyCode = item.CustomerCurrencyCode;
            line.Price = item.Price;
            line.OldPrice = item.OldPrice;
            line.DiscountValue = item.DiscountValue;
            line.Payout = item.Payout;
            line.CurrencyCode = item.CurrencyCode;
            line.CommissionAmount = item.CommissionAmount;
            line.CommissionCurrencyCode = item.CommissionCurrencyCode;
        }

        return changed;
    }

    // currency codes come from the product line itself, so they say nothing about whether amounts arrived
    private static bool HasFinancials(ExternalPostingItem item) =>
        item.CustomerPrice is not null || item.Price is not null || item.OldPrice is not null
        || item.DiscountValue is not null || item.Payout is not null || item.CommissionAmount is not null;

    private sealed record CardRow(Guid Id, string? Sku, string OfferId, Guid? CatalogItemId);

    /// <summary>One position of a posting, already resolved against the catalog; both ids are null when nothing matched.</summary>
    private sealed record OrderLine(Guid? CardId, Guid? CatalogItemId, ExternalPostingItem Item);

    private sealed class CardLookup
    {
        public Dictionary<string, CardRow> BySku { get; } = [];

        public Dictionary<string, CardRow> ByOfferId { get; } = [];

        public CardRow? Find(ExternalPostingItem item)
        {
            if (item.Sku is { Length: > 0 } sku && BySku.TryGetValue(sku, out var bySku))
                return bySku;

            return item.OfferId.Length > 0 && ByOfferId.TryGetValue(item.OfferId, out var byOfferId)
                ? byOfferId
                : null;
        }
    }
}
