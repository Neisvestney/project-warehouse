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
    IEntityLockService locks,
    IRealtimeNotifier realtime,
    IMarketplaceReturnSyncService returnSync,
    IMarketplaceAccrualSyncService accrualSync,
    IOptions<MarketplacesOptions> options,
    ILogger<MarketplaceOrderSyncService> logger) : IMarketplaceOrderSyncService
{
    /// <summary>
    /// Above this the list stops growing; OrdersSkipped keeps counting. A store with an unmapped
    /// catalog would otherwise inflate a single jsonb row into megabytes the UI never shows.
    /// </summary>
    private const int SkippedCap = 100;

    private const int CatchUpChunkSize = 200;

    /// <summary>
    /// What the history import and the FBS external import ask for: everything the marketplace will not move
    /// backwards from, plus the in-transit state, which is as close to done as a posting past the warehouse
    /// gets. Anything earlier is left to the ordinary import, which knows how to assemble it.
    /// </summary>
    private static readonly MarketplaceOrderStatus[] ExternalStatuses =
        [MarketplaceOrderStatus.Delivering, MarketplaceOrderStatus.Delivered, MarketplaceOrderStatus.Cancelled];

    private readonly OzonOptions _ozon = options.Value.Ozon;

    public async Task SyncOrdersAsync(IMarketplaceProvider provider, MarketplaceCredentials credentials,
        MarketplaceAccount account, MarketplaceSyncRun run, CancellationToken ct)
    {
        using var activity = AppTelemetry.Source.StartActivity("marketplace.sync.orders");

        var skipped = new List<SkippedOrderInfo>();

        // Confirmation needs only the FBS postings refreshed — the rest of the background follows it, so a
        // failure in, say, the returns import does not cost the button its working orders
        await RefreshFbsAsync(provider, credentials, account, run, ct);
        await ConfirmDraftsAsync(account, run, skipped, ct);

        // Saved before the remaining steps, so a failure in them keeps the reasons along with the count.
        // A fresh instance, not an in-place mutation: a jsonb scalar has no value comparer, so change
        // tracking cannot see the list grow — the same reason cards do `Barcodes = [.. …]`.
        run.SkippedOrders = skipped.Count > 0 ? [.. skipped] : null;
        await db.SaveChangesAsync(ct);

        await RefreshRestAsync(provider, credentials, account, run, ct);

        activity?.SetTag("marketplace.orders.created", run.OrdersCreated);
        activity?.SetTag("marketplace.orders.updated", run.OrdersUpdated);
        activity?.SetTag("marketplace.orders.confirmed", run.OrdersConfirmed);
        activity?.SetTag("marketplace.orders.skipped", skipped.Count);
    }

    // ── Discovery: postings handed to the seller arrive as drafts ─────────────

    private async Task DiscoverPostingsAsync(IMarketplaceProvider provider, MarketplaceCredentials credentials,
        MarketplaceAccount account, MarketplaceSyncRun run, CancellationToken ct)
    {
        var warehouses = await LoadWarehousesAsync(account.Id, ct);

        await foreach (var page in provider.FetchActivePostingsAsync(credentials, ct))
        {
            await db.Database.ExecuteInTransactionAsync("marketplace.sync.orders.page", async () =>
            {
                run.OrdersProcessed += page.Count;

                var known = await LockAndLoadKnownAsync(account.Id, page, ct);
                var cards = await LoadCardsAsync(account.Id, page, ct);

                foreach (var posting in page)
                {
                    if (known.TryGetValue(posting.PostingNumber, out var existing))
                    {
                        var changed = ApplyPosting(existing, posting);
                        // a split draft has just been deleted, and there is nothing left to rebuild
                        if (IsDraft(existing.Order) && db.Entry(existing.Order).State != EntityState.Deleted)
                            changed |= RebuildDraft(existing, posting, account.Id, warehouses, cards);
                        if (changed)
                            run.OrdersUpdated++;
                        continue;
                    }

                    // a split posting is gone, and its heirs arrive under their own numbers
                    if (posting.Status == MarketplaceOrderStatus.Split)
                        continue;

                    var lines = ResolveLines(posting, account.Id, cards);
                    db.Orders.Add(BuildOrder(posting, account, ResolveWarehouse(posting, warehouses), lines,
                        isExternal: false));
                    run.OrdersCreated++;
                }

                // per page, so the sync modal's counters advance while the run is still going
                await db.SaveChangesAsync(ct);
            }, ct);
            await realtime.PublishProgressAsync(run, ct);
        }
    }

    private static Guid? ResolveWarehouse(ExternalPosting posting, IReadOnlyDictionary<string, Guid> warehouses) =>
        posting.Scheme == ExternalPostingScheme.Fbs
        && posting.WarehouseExternalId is { } externalId
        && warehouses.TryGetValue(externalId, out var mapped)
            ? mapped
            : null;

    private static bool IsDraft(Order? order) =>
        order is { Type: OrderType.FBS, IsExternal: false, Status: OrderStatus.Draft };

    // warehouses are a handful per seller, so they are loaded once; cards are not, and go per page
    private async Task<Dictionary<string, Guid>> LoadWarehousesAsync(Guid accountId, CancellationToken ct) =>
        await db.MarketplaceWarehouses
            .Where(w => w.MarketplaceAccountId == accountId && w.WarehouseId != null && !w.IsArchived)
            .ToDictionaryAsync(w => w.ExternalId, w => w.WarehouseId!.Value, ct);

    /// <summary>
    /// Locks the orders behind the page's known postings before reading them, so the page is applied to
    /// state no user request can change until it commits. Runs inside the page's transaction.
    /// </summary>
    private async Task<Dictionary<string, MarketplaceOrder>> LockAndLoadKnownAsync(
        Guid accountId, IReadOnlyList<ExternalPosting> page, CancellationToken ct)
    {
        var numbers = page.Select(p => p.PostingNumber).ToList();

        var orderIds = await db.MarketplaceOrders
            .Where(o => o.MarketplaceAccountId == accountId && numbers.Contains(o.PostingNumber))
            .Select(o => o.OrderId)
            .ToListAsync(ct);
        await locks.LockManyAsync<Order>(orderIds, ct, IEntityLockService.BackgroundTimeout);

        return await LoadKnownAsync(accountId, page, ct);
    }

    private async Task<Dictionary<string, MarketplaceOrder>> LoadKnownAsync(
        Guid accountId, IReadOnlyList<ExternalPosting> page, CancellationToken ct)
    {
        var numbers = page.Select(p => p.PostingNumber).ToList();

        return await db.MarketplaceOrders
            .Where(o => o.MarketplaceAccountId == accountId && numbers.Contains(o.PostingNumber))
            .Include(o => o.Order)
            .ThenInclude(o => o!.MarketplaceItems)
            .ThenInclude(i => i.MarketplaceCard)
            // a draft's composition is rebuilt from the posting
            .Include(o => o.Order).ThenInclude(o => o!.Boxes).ThenInclude(b => b.Components)
            .Include(o => o.Parcels).ThenInclude(p => p.Items)
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

    /// <summary>
    /// The shared shape of an imported posting: an FBS draft, or an external order already past the warehouse.
    /// Both carry every position, with box components for whatever the catalog recognized and nothing for the rest.
    /// </summary>
    private static Order BuildOrder(ExternalPosting posting, MarketplaceAccount account,
        Guid? warehouseId, IReadOnlyList<OrderLine> lines, bool isExternal)
    {
        var now = DateTime.UtcNow;
        var orderId = Guid.NewGuid();
        var boxId = Guid.NewGuid();
        var status = isExternal
            ? ExternalOrderStatus(posting.Status, posting.Cancellation?.CancelledAfterShip)
            : OrderStatus.Draft;
        var items = lines.Select(l => BuildItem(orderId, l)).ToList();

        return new Order
        {
            Id = orderId,
            Type = posting.Scheme == ExternalPostingScheme.Fbo ? OrderType.FboPosting : OrderType.FBS,
            Status = status,
            IsExternal = isExternal,
            WarehouseId = warehouseId,
            PlannedShipmentAt = posting.ShipmentDate,
            CreatedAt = now,
            ShippedAt = status == OrderStatus.Shipped ? posting.InProcessAt ?? posting.CreatedAt ?? now : null,
            // created by the integration; who started the run is recorded on MarketplaceSyncRun
            CreatedById = null,
            MarketplaceItems = items,
            Boxes =
            [
                new OrderBox
                {
                    Id = boxId,
                    OrderId = orderId,
                    // Always one box, even when MultiBoxQty > 1: the marketplace says how many packages
                    // but not what goes in which, so the packer splits them during assembly.
                    Components = BuildComponents(boxId, lines),
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
                OzonScanitBarcode = posting.Scanit,
                Parcels = BuildParcels(orderId, posting, items, boxId),
                StatusSyncedAt = now,
                SyncedAt = now,
            },
        };
    }

    private static OrderMarketplaceItem BuildItem(Guid orderId, OrderLine line) => new()
    {
        Id = Guid.NewGuid(),
        OrderId = orderId,
        MarketplaceCardId = line.CardId,
        CatalogItemId = line.CatalogItemId,
        Quantity = line.Item.Quantity,
        CustomerPrice = line.Item.CustomerPrice,
        CustomerCurrencyCode = line.Item.CustomerCurrencyCode,
        Price = line.Item.Price,
        OldPrice = line.Item.OldPrice,
        DiscountValue = line.Item.DiscountValue,
        Payout = line.Item.Payout,
        CurrencyCode = line.Item.CurrencyCode,
        CommissionAmount = line.Item.CommissionAmount,
        CommissionCurrencyCode = line.Item.CommissionCurrencyCode,
    };

    /// <summary>
    /// Collapsed by catalog item, not by card: two cards may point at the same item. Unrecognized positions
    /// put nothing in the box.
    /// </summary>
    private static List<OrderBoxComponent> BuildComponents(Guid boxId, IReadOnlyList<OrderLine> lines) =>
    [
        .. lines
            .Where(l => l.CatalogItemId is not null)
            .GroupBy(l => l.CatalogItemId!.Value)
            .Select(g => new OrderBoxComponent
            {
                Id = Guid.NewGuid(),
                OrderBoxId = boxId,
                CatalogItemId = g.Key,
                Quantity = g.Sum(l => l.Item.Quantity),
            }),
    ];

    /// <summary>
    /// A posting the marketplace states no layout for is one parcel, the whole posting, coinciding with the one
    /// box. A stated layout maps its contents onto <paramref name="items"/>, which are in posting order.
    /// </summary>
    private static List<MarketplaceParcel> BuildParcels(Guid orderId, ExternalPosting posting,
        IReadOnlyList<OrderMarketplaceItem> items, Guid? boxId)
    {
        if (posting.Parcels is not { Count: > 0 } layout)
            return [new MarketplaceParcel { Id = Guid.NewGuid(), MarketplaceOrderId = orderId, Index = 1, OrderBoxId = boxId }];

        return
        [
            .. layout.Select((parcel, i) =>
            {
                var parcelId = Guid.NewGuid();
                return new MarketplaceParcel
                {
                    Id = parcelId,
                    MarketplaceOrderId = orderId,
                    Index = i + 1,
                    ExternalId = parcel.ExternalId,
                    Barcode = parcel.Barcode,
                    OrderBoxId = layout.Count == 1 ? boxId : null,
                    Items =
                    [
                        .. parcel.Items
                            .Where(item => item.ItemIndex >= 0 && item.ItemIndex < items.Count)
                            .Select(item => new MarketplaceParcelItem
                            {
                                MarketplaceParcelId = parcelId,
                                OrderMarketplaceItemId = items[item.ItemIndex].Id,
                                Quantity = item.Quantity,
                                PartIndex = item.PartIndex,
                                PartCount = item.PartCount,
                            }),
                    ],
                };
            }),
        ];
    }

    /// <summary>
    /// A draft has no work behind it, so it follows the posting whole: lines, box and parcels are brought to
    /// what the posting holds now, the catalog snapshot to what the cards are mapped to now. Lines that already
    /// match keep their rows — and with them their links to returns and accruals; an Ozon parcel keeps its label.
    /// </summary>
    private bool RebuildDraft(MarketplaceOrder known, ExternalPosting posting, Guid accountId,
        IReadOnlyDictionary<string, Guid> warehouses, CardLookup cards)
    {
        var order = known.Order;
        var lines = ResolveLines(posting, accountId, cards);
        var changed = false;

        var warehouseId = ResolveWarehouse(posting, warehouses);
        if (order.WarehouseId != warehouseId)
        {
            order.WarehouseId = warehouseId;
            changed = true;
        }

        // in posting order, which parcel contents index into
        var items = MatchLines(order.MarketplaceItems, lines);
        var sameLines = items is not null;
        if (items is not null)
        {
            foreach (var (item, line) in items.Zip(lines).Where(p => p.First.CatalogItemId != p.Second.CatalogItemId))
            {
                item.CatalogItemId = line.CatalogItemId;
                changed = true;
            }
        }
        else
        {
            db.OrderMarketplaceItems.RemoveRange(order.MarketplaceItems);
            items = [.. lines.Select(l => BuildItem(order.Id, l))];
            db.OrderMarketplaceItems.AddRange(items);
            changed = true;
        }

        var box = order.Boxes.OrderBy(b => b.Id).FirstOrDefault();
        if (box is null)
        {
            box = new OrderBox { Id = Guid.NewGuid(), OrderId = order.Id };
            db.OrderBoxes.Add(box);
            changed = true;
        }

        foreach (var extra in order.Boxes.Where(b => b.Id != box.Id).ToList())
        {
            db.OrderBoxes.Remove(extra);
            changed = true;
        }

        var components = BuildComponents(box.Id, lines);
        var sameComponents = box.Components.Count == components.Count
                             && components.TrueForAll(c => box.Components.Any(e =>
                                 e.CatalogItemId == c.CatalogItemId && e.Quantity == c.Quantity));
        if (!sameComponents)
        {
            db.OrderBoxComponents.RemoveRange(box.Components);
            db.OrderBoxComponents.AddRange(components);
            changed = true;
        }

        changed |= RebuildParcels(known, posting, items, box.Id, linesReplaced: !sameLines);
        return changed;
    }

    /// <summary>
    /// Pairs every line with a stored row of the same card and quantity, in line order; null when the
    /// composition differs. Rows load in no particular order, so the pairing goes by content, not position.
    /// </summary>
    private static List<OrderMarketplaceItem>? MatchLines(ICollection<OrderMarketplaceItem> stored,
        IReadOnlyList<OrderLine> lines)
    {
        if (stored.Count != lines.Count)
            return null;

        var free = stored.ToList();
        var matched = new List<OrderMarketplaceItem>(lines.Count);
        foreach (var line in lines)
        {
            var index = free.FindIndex(i => i.MarketplaceCardId == line.CardId && i.Quantity == line.Item.Quantity);
            if (index < 0)
                return null;

            matched.Add(free[index]);
            free.RemoveAt(index);
        }

        return matched;
    }

    /// <summary>
    /// A layout-less posting keeps its one parcel and the label on it. A stated layout is replaced whenever it
    /// differs — the marketplace allows changing it until packing is done, and a label of an old place is void.
    /// </summary>
    private bool RebuildParcels(MarketplaceOrder known, ExternalPosting posting,
        IReadOnlyList<OrderMarketplaceItem> items, Guid boxId, bool linesReplaced)
    {
        var desired = BuildParcels(known.OrderId, posting, items, boxId);

        if (posting.Parcels is not { Count: > 0 } && known.Parcels is [{ Index: 1, Items.Count: 0 } single])
        {
            if (single.OrderBoxId == boxId)
                return false;
            single.OrderBoxId = boxId;
            return true;
        }

        if (!linesReplaced && SameLayout(known.Parcels, desired))
            return false;

        db.MarketplaceParcels.RemoveRange(known.Parcels);
        db.MarketplaceParcels.AddRange(desired);
        known.Parcels = desired;
        return true;
    }

    private static bool SameLayout(IReadOnlyList<MarketplaceParcel> current, IReadOnlyList<MarketplaceParcel> desired) =>
        current.Count == desired.Count
        && current.OrderBy(p => p.Index).Zip(desired).All(p =>
            p.First.ExternalId == p.Second.ExternalId
            && p.First.Barcode == p.Second.Barcode
            && p.First.Items.Count == p.Second.Items.Count
            && p.Second.Items.TrueForAll(d => p.First.Items.Any(c =>
                c.OrderMarketplaceItemId == d.OrderMarketplaceItemId
                && c.Quantity == d.Quantity && c.PartIndex == d.PartIndex && c.PartCount == d.PartCount)));

    /// <summary>
    /// The idempotency contract: marketplace-owned fields only. Boxes, components, assembly tasks and
    /// fulfillments are never touched — by the time a posting is re-seen they may be half assembled. A draft's
    /// composition is the exception, rebuilt separately by <see cref="RebuildDraft"/>.
    /// </summary>
    private bool ApplyPosting(MarketplaceOrder known, ExternalPosting posting)
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
            || (posting.Scanit is not null && known.OzonScanitBarcode != posting.Scanit)
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
        known.OzonScanitBarcode = posting.Scanit ?? known.OzonScanitBarcode;
        known.ExternalOrderNumber = posting.ExternalOrderNumber;
        changed |= FollowMarketplace(known);

        known.StatusSyncedAt = now;
        known.SyncedAt = now;

        return changed;
    }

    /// <summary>
    /// An external order whose goods never left the seller — cancelled before shipment, or an FBO sale that
    /// fell through — did not happen, so it is Canceled; every other one is Shipped.
    /// </summary>
    private static OrderStatus ExternalOrderStatus(MarketplaceOrderStatus status, bool? cancelledAfterShip) =>
        status == MarketplaceOrderStatus.Cancelled && cancelledAfterShip != true
            ? OrderStatus.Canceled
            : OrderStatus.Shipped;

    /// <summary>
    /// External orders follow the marketplace; working ones never do. Must run after the marketplace fields
    /// are overwritten.
    /// </summary>
    private static void ApplyExternalOrderStatus(MarketplaceOrder known)
    {
        if (known.Order is not { IsExternal: true } order)
            return;

        var status = ExternalOrderStatus(known.Status, known.CancelledAfterShip);
        if (order.Status == status)
            return;

        order.Status = status;
        order.ShippedAt = status == OrderStatus.Shipped ? known.InProcessAt ?? order.CreatedAt : null;
    }

    /// <summary>
    /// The order statuses the sync owns follow the marketplace: an external order's, and a draft's until it
    /// closes. A draft whose posting went past assembly without WMS becomes external; a split one is deleted,
    /// its heirs arriving as drafts of their own. Must run after the marketplace fields are overwritten.
    /// Returns whether a draft closed.
    /// </summary>
    private bool FollowMarketplace(MarketplaceOrder known)
    {
        var closed = false;

        if (IsDraft(known.Order))
        {
            switch (known.Status)
            {
                case MarketplaceOrderStatus.Split:
                    db.Orders.Remove(known.Order);
                    return true;

                case MarketplaceOrderStatus.Delivering or MarketplaceOrderStatus.Delivered
                    or MarketplaceOrderStatus.Cancelled or MarketplaceOrderStatus.Arbitration:
                    known.Order.IsExternal = true;
                    closed = true;
                    break;
            }
        }

        ApplyExternalOrderStatus(known);
        return closed;
    }

    // ── Working import: drafts the marketplace has packed go to work ──────────

    /// <summary>
    /// The only crossing from the sync's side to the warehouse's: a draft in awaiting_deliver becomes a working
    /// order once its warehouse and every position map. One that does not stays a draft and lands in the
    /// skipped list — that is what someone has to be looking at when it happens. The marketplace is not asked:
    /// only drafts discovery has rebuilt in this same run qualify. One the status catch-up alone refreshed was
    /// not in the unfulfilled list, so its composition is stale: it is skipped too, and waits for a run that sees it.
    /// </summary>
    private async Task ConfirmDraftsAsync(MarketplaceAccount account, MarketplaceSyncRun run,
        List<SkippedOrderInfo> skipped, CancellationToken ct)
    {
        var candidates = await DraftsAwaitingDeliver(account.Id)
            .Select(o => new { o.OrderId, o.PostingNumber, Rebuilt = o.SyncedAt >= run.StartedAt })
            .ToListAsync(ct);

        foreach (var stale in candidates.Where(c => !c.Rebuilt))
            Skip(new SkippedOrderInfo
            {
                PostingNumber = stale.PostingNumber,
                Reason = ErrorCode.MarketplaceOrderDraftNotRefreshed,
            });

        var draftIds = candidates.Where(c => c.Rebuilt).Select(c => c.OrderId).ToList();

        foreach (var chunk in draftIds.Chunk(CatchUpChunkSize))
        {
            await db.Database.ExecuteInTransactionAsync("marketplace.sync.orders.confirm", async () =>
            {
                var orderIds = chunk.ToList();
                await locks.LockManyAsync<Order>(orderIds, ct, IEntityLockService.BackgroundTimeout);

                // the filter again: a draft closed since it was listed is no longer one to confirm
                var drafts = await DraftsAwaitingDeliver(account.Id)
                    .Where(o => orderIds.Contains(o.OrderId) && o.SyncedAt >= run.StartedAt)
                    .Include(o => o.Order)
                    .ThenInclude(o => o!.MarketplaceItems)
                    .ThenInclude(i => i.MarketplaceCard)
                    .ToListAsync(ct);

                var now = DateTime.UtcNow;
                foreach (var draft in drafts)
                {
                    if (ConfirmGate(draft) is { } skip)
                    {
                        Skip(skip);
                        continue;
                    }

                    draft.Order.Status = OrderStatus.Confirmed;
                    draft.Order.ConfirmedAt = now;
                    run.OrdersConfirmed++;
                }

                await db.SaveChangesAsync(ct);
            }, ct);
            await realtime.PublishProgressAsync(run, ct);
        }

        void Skip(SkippedOrderInfo skip)
        {
            run.OrdersSkipped++;
            if (skipped.Count < SkippedCap)
                skipped.Add(skip);
        }
    }

    // ApplyPosting stamps SyncedAt, the status catch-up only StatusSyncedAt — so SyncedAt tells a rebuilt draft apart
    private IQueryable<MarketplaceOrder> DraftsAwaitingDeliver(Guid accountId) =>
        db.MarketplaceOrders.Where(o => o.MarketplaceAccountId == accountId
                                        && o.Status == MarketplaceOrderStatus.AwaitingDeliver
                                        && o.Order.Type == OrderType.FBS
                                        && !o.Order.IsExternal
                                        && o.Order.Status == OrderStatus.Draft);

    /// <summary>
    /// The warehouse first, then the catalog. The draft was rebuilt against the current mappings on discovery,
    /// so a null warehouse or catalog snapshot means the mapping is missing now.
    /// </summary>
    private static SkippedOrderInfo? ConfirmGate(MarketplaceOrder draft)
    {
        if (draft.Order.WarehouseId is null)
            return new SkippedOrderInfo
            {
                PostingNumber = draft.PostingNumber,
                Reason = ErrorCode.MarketplaceOrderWarehouseNotMapped,
            };

        var unmapped = draft.Order.MarketplaceItems
            .Where(i => i.CatalogItemId is null)
            .Select(i => i.MarketplaceCard?.OfferId ?? "")
            .Distinct()
            .ToList();

        return unmapped.Count > 0
            ? new SkippedOrderInfo
            {
                PostingNumber = draft.PostingNumber,
                Reason = ErrorCode.MarketplaceOrderCardNotMapped,
                OfferIds = unmapped,
            }
            : null;
    }

    // ── Unattended refresh ────────────────────────────────────────────────────

    public async Task SyncOrdersBackgroundAsync(IMarketplaceProvider provider, MarketplaceCredentials credentials,
        MarketplaceAccount account, MarketplaceSyncRun run, CancellationToken ct)
    {
        using var activity = AppTelemetry.Source.StartActivity("marketplace.sync.orders_background");

        await RefreshFbsAsync(provider, credentials, account, run, ct);
        await RefreshRestAsync(provider, credentials, account, run, ct);
    }

    /// <summary>
    /// Drafts discovered and rebuilt, then every open FBS posting asked about — after it a draft still awaiting
    /// delivery is one the marketplace really has in that state.
    /// </summary>
    private async Task RefreshFbsAsync(IMarketplaceProvider provider, MarketplaceCredentials credentials,
        MarketplaceAccount account, MarketplaceSyncRun run, CancellationToken ct)
    {
        await DiscoverPostingsAsync(provider, credentials, account, run, ct);
        await CatchUpStatusesAsync(provider, credentials, account, run, ExternalPostingScheme.Fbs, ct);
    }

    private async Task RefreshRestAsync(IMarketplaceProvider provider, MarketplaceCredentials credentials,
        MarketplaceAccount account, MarketplaceSyncRun run, CancellationToken ct)
    {
        await ImportFbsPostingsAsync(provider, credentials, account, run, ct);
        await ImportFboPostingsAsync(provider, credentials, account, run, ct);
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
    /// FBS postings that got past the warehouse without WMS ever importing them — cancelled before
    /// awaiting_deliver, or shipped while the working import skipped them. They are asked for by status change
    /// since the last import rather than by creation, because a posting may be cancelled days after it was made.
    /// </summary>
    private async Task ImportFbsPostingsAsync(IMarketplaceProvider provider, MarketplaceCredentials credentials,
        MarketplaceAccount account, MarketplaceSyncRun run, CancellationToken ct)
    {
        var to = DateTime.UtcNow;
        var createdSince = to.AddDays(-_ozon.FbsImportWindowPastDays);
        var changedSince = account.FbsPostingsSyncedAt is { } syncedAt
            ? syncedAt.AddHours(-_ozon.FbsImportOverlapHours)
            : createdSince;

        await ImportExternalPostingsAsync(provider, credentials, account, run,
            new ExternalPostingQuery(ExternalPostingScheme.Fbs, createdSince, to, ExternalStatuses, changedSince),
            refreshKnown: false, ct);

        // same rule as FboPostingsSyncedAt: a run that threw halfway leaves the mark where it was
        account.FbsPostingsSyncedAt = to;
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

        var openKeys = await db.MarketplaceOrders
            .Where(o => o.MarketplaceAccountId == account.Id
                        && o.Status != MarketplaceOrderStatus.Delivered
                        && o.Status != MarketplaceOrderStatus.Cancelled
                        && o.Status != MarketplaceOrderStatus.Split
                        // the two schemes are asked about through different endpoints
                        && (o.Order!.Type == OrderType.FboPosting) == isFbo
                        // phase 1 just refreshed everything the unfulfilled list returned; re-asking would
                        // cost one single-posting call per open order, every run, for no new information
                        && o.StatusSyncedAt < run.StartedAt)
            .Select(o => new { o.OrderId, o.PostingNumber })
            .ToListAsync(ct);

        if (openKeys.Count == 0)
            return;

        // asked before the transaction opens, so no lock is held across the marketplace call
        var statuses = (await provider.FetchPostingStatusesAsync(
                credentials, [.. openKeys.Select(o => o.PostingNumber)], scheme, ct))
            .ToDictionary(s => s.PostingNumber);

        // chunked like the discovery pages: one transaction over every open order would hold them all at once
        foreach (var chunk in openKeys.Chunk(CatchUpChunkSize))
        {
            await db.Database.ExecuteInTransactionAsync("marketplace.sync.orders.catch_up", async () =>
            {
                var orderIds = chunk.Select(o => o.OrderId).ToList();
                await locks.LockManyAsync<Order>(orderIds, ct, IEntityLockService.BackgroundTimeout);

                // the filters again: an order closed while the marketplace was being asked must not be reopened
                var open = await db.MarketplaceOrders
                    .Where(o => orderIds.Contains(o.OrderId)
                                && o.Status != MarketplaceOrderStatus.Delivered
                                && o.Status != MarketplaceOrderStatus.Cancelled
                                && o.Status != MarketplaceOrderStatus.Split
                                && o.StatusSyncedAt < run.StartedAt)
                    .Include(o => o.Order)
                    .ThenInclude(o => o!.MarketplaceItems)
                    .ThenInclude(i => i.MarketplaceCard)
                    .AsSplitQuery()
                    .ToListAsync(ct);

                ApplyStatuses(open, statuses, run);
                await db.SaveChangesAsync(ct);
            }, ct);
        }
    }

    private void ApplyStatuses(List<MarketplaceOrder> open, Dictionary<string, ExternalPostingStatus> statuses,
        MarketplaceSyncRun run)
    {
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

            var counted = cancellationChanged
                          || financialsChanged
                          || order.Status != status.Status
                          || order.RawStatus != status.RawStatus
                          || order.RawSubstatus != status.RawSubstatus
                          || order.TrackingNumber != status.TrackingNumber;
            if (counted)
                run.OrdersUpdated++;

            StampFinalStatus(order, status.Status, now);
            order.Status = status.Status;
            order.RawStatus = status.RawStatus;
            order.RawSubstatus = status.RawSubstatus;
            order.TrackingNumber = status.TrackingNumber ?? order.TrackingNumber;
            // a draft that closes counts as updated even when nothing else moved
            if (FollowMarketplace(order) && !counted)
                run.OrdersUpdated++;
            order.StatusSyncedAt = now;
        }
    }

    // ── History backfill ──────────────────────────────────────────────────────

    public async Task SyncOrdersBackfillAsync(IMarketplaceProvider provider, MarketplaceCredentials credentials,
        MarketplaceAccount account, MarketplaceSyncRun run, CancellationToken ct)
    {
        using var activity = AppTelemetry.Source.StartActivity("marketplace.sync.orders_backfill");

        if (run.BackfillSince is not { } since || run.BackfillTo is not { } to)
            throw new ValidationException("since", ErrorCode.Required,
                "A backfill run needs both ends of its period.");

        foreach (var scheme in (ExternalPostingScheme[])[ExternalPostingScheme.Fbs, ExternalPostingScheme.Fbo])
            await ImportExternalPostingsAsync(provider, credentials, account, run,
                new ExternalPostingQuery(scheme, since, to, ExternalStatuses), refreshKnown: false, ct);

        await returnSync.SyncReturnsBackfillAsync(provider, credentials, account, run, since, to, ct);
        // last, so the postings its accruals belong to are already in
        await accrualSync.SyncAccrualsBackfillAsync(provider, credentials, account, run, since, to, ct);

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
            await db.Database.ExecuteInTransactionAsync("marketplace.sync.orders.external_page", async () =>
            {
                run.OrdersProcessed += page.Count;

                // without a refresh known orders are only looked up, never written, so they need no lock
                var known = refreshKnown
                    ? await LockAndLoadKnownAsync(account.Id, page, ct)
                    : await LoadKnownAsync(account.Id, page, ct);
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
                    db.Orders.Add(BuildOrder(posting, account, ResolveWarehouse(posting, warehouses), lines,
                        isExternal: true));
                    run.OrdersCreated++;
                }

                await db.SaveChangesAsync(ct);
            }, ct);
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
