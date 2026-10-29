using EntityFrameworkCore.Projectables;

namespace ProjectWarehouse.Server.Domain;

/// <summary>
/// Marketplace extension of an <see cref="Order"/>, 1:1 on a shared primary key.
/// </summary>
/// <remarks>
/// Deliberately not <c>IHasIdentity</c>: the entity is never addressed over HTTP, never appears in a
/// list, and its change history is kept on the order. It also holds the one thing the orders domain had
/// no room for — which account's credentials to use when fetching a label.
/// </remarks>
public class MarketplaceOrder
{
    public Guid OrderId { get; set; }
    public Order Order { get; set; } = null!;

    public Guid MarketplaceAccountId { get; set; }
    public MarketplaceAccount MarketplaceAccount { get; set; } = null!;

    public string PostingNumber { get; set; } = null!;

    /// <summary>Number of the marketplace order this posting belongs to.</summary>
    public string? ExternalOrderNumber { get; set; }

    /// <summary>
    /// Wildberries' <c>rid</c> of the assembly task — the <c>srid</c> of its finance report rows, which name no
    /// posting when their <c>orderId</c> is <c>0</c>. Once stored it is never cleared; null for other marketplaces.
    /// </summary>
    public string? Rid { get; set; }

    /// <summary>
    /// Rebuilt by the history backfill from the finance report after the posting API stopped keeping it: the status
    /// is inferred from the money and the price is the report's.
    /// </summary>
    public bool IsReconstructed { get; set; }

    public MarketplaceOrderStatus Status { get; set; }

    /// <summary>
    /// When a sync first saw <see cref="Status"/> become Delivered — the marketplace states no delivery moment of
    /// its own, so this is accurate to the account's sync interval. A posting imported already delivered gets the
    /// Moscow midnight of its delivery day from the accrual journal once the accrual sync has it, null until then.
    /// </summary>
    public DateTime? DeliveredAt { get; set; }

    /// <summary>Same as <see cref="DeliveredAt"/>, for Cancelled.</summary>
    public DateTime? CancelledAt { get; set; }

    // diagnostics only — the normalized Status is what the UI and queries use
    public string? RawStatus { get; set; }
    public string? RawSubstatus { get; set; }

    /// <summary>
    /// Null while the posting is alive. Set once it is cancelled: <c>true</c> when the posting had already
    /// been packed (Ozon's <c>cancelled_after_ship</c>, despite the name), whether or not it was handed over —
    /// <see cref="DeliveringAt"/> tells those apart.
    /// </summary>
    public bool? CancelledAfterAssembly { get; set; }

    public MarketplaceCancellationType? CancellationType { get; set; }

    // diagnostics only, same as RawStatus — a type the provider could not collapse survives here
    public string? RawCancellationType { get; set; }

    /// <summary>Cancellation reason as worded by the marketplace.</summary>
    public string? CancelReason { get; set; }

    /// <summary>
    /// <see cref="CancelReason"/> in the cross-marketplace vocabulary. Null when the marketplace gave no reason.
    /// </summary>
    public MarketplaceLossReason? CancelReasonCategory { get; set; }

    // diagnostics only — the marketplace's own reason code, where it has one
    public string? RawCancelReasonCode { get; set; }

    /// <summary>Deadline the marketplace expects the posting to be packed by.</summary>
    public DateTime? ShipmentDate { get; set; }

    public DateTime? InProcessAt { get; set; }

    /// <summary>
    /// When the seller handed the posting over to delivery, as the marketplace states it; kept after a
    /// cancellation. Always null for FBO.
    /// </summary>
    public DateTime? DeliveringAt { get; set; }

    /// <summary>Recommended ship-by time, past which the marketplace applies a late tariff.</summary>
    public DateTime? ShipmentDateWithoutDelay { get; set; }

    /// <summary>Planned delivery window to the buyer.</summary>
    public DateTime? DeliveryDateBegin { get; set; }

    public DateTime? DeliveryDateEnd { get; set; }

    public string? TrackingNumber { get; set; }
    public string? DeliveryMethodName { get; set; }

    /// <summary>
    /// The marketplace's warehouse the posting ships from, as <see cref="MarketplaceWarehouse.ExternalId"/>.
    /// Kept so an external order imported before its warehouse was mapped gets the WMS warehouse once it is.
    /// Null for FBO.
    /// </summary>
    public string? WarehouseExternalId { get; set; }

    /// <summary>How many packages the marketplace expects. A hint for the packer, not a box count in WMS.</summary>
    public int MultiBoxQty { get; set; } = 1;

    /// <summary>
    /// Number printed on the marketplace label: Ozon's scanit, which ties a label page back to this posting,
    /// or the Wildberries sticker number (<c>partA</c> + <c>partB</c>). Ozon states the scanit only while the
    /// posting is <c>awaiting_deliver</c> and Wildberries the sticker only with the label or in the archive,
    /// so once stored it is never cleared.
    /// </summary>
    public string? LabelBarcode { get; set; }

    /// <summary>
    /// The supply the marketplace put the posting in. Sync-owned: it follows the marketplace, including a move to
    /// another supply. Null where the marketplace has no supplies or the posting is not in one yet.
    /// </summary>
    public Guid? SupplyId { get; set; }
    public MarketplaceSupply? Supply { get; set; }

    /// <summary>At least one; the labels live on them.</summary>
    public List<MarketplaceParcel> Parcels { get; set; } = [];

    public DateTime StatusSyncedAt { get; set; }
    public DateTime SyncedAt { get; set; }

    /// <summary>
    /// Whether the buyer sent the order back, by the units of <see cref="Domain.Order.ReturnedQuantity"/>. Mapped in
    /// memory it needs <c>Order.MarketplaceReturns</c> and <c>Order.MarketplaceItems</c> loaded, or it reads None.
    /// </summary>
    [Projectable]
    public MarketplaceOrderReturnState ReturnState =>
        Order.ReturnedQuantity == 0
            ? MarketplaceOrderReturnState.None
            : Order.ReturnedQuantity >= Order.MarketplaceQuantity
                ? MarketplaceOrderReturnState.Full
                : MarketplaceOrderReturnState.Partial;
}
