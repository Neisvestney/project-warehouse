using Microsoft.EntityFrameworkCore;
using ProjectWarehouse.Server.Data;
using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Infrastructure;
using ProjectWarehouse.Server.Infrastructure.Observability;
using ProjectWarehouse.Server.Infrastructure.Realtime;
using ProjectWarehouse.Server.Integrations.Abstractions;
using ProjectWarehouse.Server.Integrations.Sync;
using ProjectWarehouse.Server.Models;

namespace ProjectWarehouse.Server.Services;

public class MarketplaceOrderSyncService(
    ApplicationDbContext db,
    IRealtimeNotifier realtime,
    ILogger<MarketplaceOrderSyncService> logger) : IMarketplaceOrderSyncService
{
    /// <summary>
    /// Above this the list stops growing; OrdersSkipped keeps counting. A store with an unmapped
    /// catalog would otherwise inflate a single jsonb row into megabytes the UI never shows.
    /// </summary>
    private const int SkippedCap = 100;

    public async Task SyncOrdersAsync(IMarketplaceProvider provider, MarketplaceCredentials credentials,
        MarketplaceAccount account, MarketplaceSyncRun run, CancellationToken ct)
    {
        using var activity = AppTelemetry.Source.StartActivity("marketplace.sync.orders");

        var skipped = new List<SkippedOrderInfo>();

        await DiscoverPostingsAsync(provider, credentials, account, run, skipped, ct);
        await CatchUpStatusesAsync(provider, credentials, account, run, ct);

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
        // warehouses are a handful per seller, so they are loaded once; cards are not, and go per page
        var warehouses = await db.MarketplaceWarehouses
            .Where(w => w.MarketplaceAccountId == account.Id && w.WarehouseId != null && !w.IsArchived)
            .ToDictionaryAsync(w => w.ExternalId, w => w.WarehouseId!.Value, ct);

        await foreach (var page in provider.FetchActivePostingsAsync(credentials, ct))
        {
            run.OrdersProcessed += page.Count;

            var numbers = page.Select(p => p.PostingNumber).ToList();
            var known = await db.MarketplaceOrders
                .Where(o => o.MarketplaceAccountId == account.Id && numbers.Contains(o.PostingNumber))
                .Include(o => o.Order)
                .ToDictionaryAsync(o => o.PostingNumber, ct);

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

        var bySku = new Dictionary<string, CardRow>();
        var byOfferId = new Dictionary<string, CardRow>();

        foreach (var row in rows)
        {
            if (row.Sku is { Length: > 0 })
                bySku.TryAdd(row.Sku, row);

            // offer_id is unique per Ozon account in practice, but nothing in the schema enforces it
            if (row.OfferId.Length > 0 && !byOfferId.TryAdd(row.OfferId, row))
                logger.LogWarning("Account {AccountId} has more than one card with offer_id {OfferId}",
                    accountId, row.OfferId);
        }

        return new CardLookup(bySku, byOfferId);
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

        var resolved = new List<(CardRow Card, int Quantity)>(posting.Items.Count);
        var unmapped = new List<string>();

        foreach (var item in posting.Items)
        {
            var card = cards.Find(item);
            if (card is null || card.CatalogItemId is null)
                unmapped.Add(item.OfferId);
            else
                resolved.Add((card, item.Quantity));
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

        var now = DateTime.UtcNow;
        var orderId = Guid.NewGuid();
        var boxId = Guid.NewGuid();

        order = new Order
        {
            Id = orderId,
            Type = OrderType.FBS,
            // the posting arrives already packed on the marketplace side, so it is ready to assemble
            Status = OrderStatus.Confirmed,
            WarehouseId = warehouseId,
            PlannedShipmentAt = posting.ShipmentDate,
            CreatedAt = now,
            // created by the integration; who started the run is recorded on MarketplaceSyncRun
            CreatedById = null,
            MarketplaceItems = [.. resolved.Select(r => new OrderMarketplaceItem
            {
                Id = Guid.NewGuid(),
                OrderId = orderId,
                MarketplaceCardId = r.Card.Id,
                Quantity = r.Quantity,
            })],
            Boxes =
            [
                new OrderBox
                {
                    Id = boxId,
                    OrderId = orderId,
                    // Always one box, even when MultiBoxQty > 1: the marketplace says how many packages
                    // but not what goes in which, so the packer splits them during assembly.
                    Components = [.. resolved
                        .GroupBy(r => r.Card.CatalogItemId!.Value)
                        .Select(g => new OrderBoxComponent
                        {
                            Id = Guid.NewGuid(),
                            OrderBoxId = boxId,
                            CatalogItemId = g.Key,
                            Quantity = g.Sum(r => r.Quantity),
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
                ShipmentDate = posting.ShipmentDate,
                InProcessAt = posting.InProcessAt,
                TrackingNumber = posting.TrackingNumber,
                DeliveryMethodName = posting.DeliveryMethodName,
                MultiBoxQty = posting.MultiBoxQty,
                StatusSyncedAt = now,
                SyncedAt = now,
            },
        };

        skip = null;
        return true;
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
            || known.ExternalOrderNumber != posting.ExternalOrderNumber;

        if (known.ShipmentDate != posting.ShipmentDate && known.Order is not null)
            known.Order.PlannedShipmentAt = posting.ShipmentDate;

        known.Status = posting.Status;
        known.RawStatus = posting.RawStatus;
        known.RawSubstatus = posting.RawSubstatus;
        known.ShipmentDate = posting.ShipmentDate;
        known.InProcessAt = posting.InProcessAt;
        known.TrackingNumber = posting.TrackingNumber;
        known.DeliveryMethodName = posting.DeliveryMethodName;
        known.MultiBoxQty = posting.MultiBoxQty;
        known.ExternalOrderNumber = posting.ExternalOrderNumber;

        var now = DateTime.UtcNow;
        known.StatusSyncedAt = now;
        known.SyncedAt = now;

        return changed;
    }

    // ── Phase 2: status catch-up ──────────────────────────────────────────────

    /// <summary>
    /// Postings that leave <c>awaiting_deliver</c> disappear from the unfulfilled list, and "shipped" is
    /// indistinguishable from "cancelled" by absence alone — so open ones are asked about directly.
    /// </summary>
    private async Task CatchUpStatusesAsync(IMarketplaceProvider provider, MarketplaceCredentials credentials,
        MarketplaceAccount account, MarketplaceSyncRun run, CancellationToken ct)
    {
        var open = await db.MarketplaceOrders
            .Where(o => o.MarketplaceAccountId == account.Id
                        && o.Status != MarketplaceOrderStatus.Delivered
                        && o.Status != MarketplaceOrderStatus.Cancelled
                        // phase 1 just refreshed everything the unfulfilled list returned; re-asking would
                        // cost one single-posting call per open order, every run, for no new information
                        && o.StatusSyncedAt < run.StartedAt)
            .ToListAsync(ct);

        if (open.Count == 0)
            return;

        var statuses = (await provider.FetchPostingStatusesAsync(
                credentials, [.. open.Select(o => o.PostingNumber)], ct))
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

            if (order.Status != status.Status
                || order.RawStatus != status.RawStatus
                || order.RawSubstatus != status.RawSubstatus
                || order.TrackingNumber != status.TrackingNumber)
                run.OrdersUpdated++;

            order.Status = status.Status;
            order.RawStatus = status.RawStatus;
            order.RawSubstatus = status.RawSubstatus;
            order.TrackingNumber = status.TrackingNumber ?? order.TrackingNumber;
            order.StatusSyncedAt = now;
        }

        await db.SaveChangesAsync(ct);
    }

    private sealed record CardRow(Guid Id, string? Sku, string OfferId, Guid? CatalogItemId);

    private sealed record CardLookup(
        Dictionary<string, CardRow> BySku,
        Dictionary<string, CardRow> ByOfferId)
    {
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
