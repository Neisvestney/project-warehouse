using System.ComponentModel.DataAnnotations.Schema;
using EntityFrameworkCore.Projectables;
using ProjectWarehouse.Server.Models;

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

    public MarketplaceOrderStatus Status { get; set; }

    /// <summary>
    /// When a sync first saw <see cref="Status"/> become Delivered — the marketplace states no delivery moment of
    /// its own, so this is accurate to the account's sync interval. Null for a posting imported already delivered.
    /// </summary>
    public DateTime? DeliveredAt { get; set; }

    /// <summary>Same as <see cref="DeliveredAt"/>, for Cancelled.</summary>
    public DateTime? CancelledAt { get; set; }

    // diagnostics only — the normalized Status is what the UI and queries use
    public string? RawStatus { get; set; }
    public string? RawSubstatus { get; set; }

    /// <summary>
    /// Null while the posting is alive. Set once it is cancelled: <c>true</c> when the marketplace had
    /// already taken the shipment, which <see cref="Status"/> alone cannot tell apart.
    /// </summary>
    public bool? CancelledAfterShip { get; set; }

    public MarketplaceCancellationType? CancellationType { get; set; }

    // diagnostics only, same as RawStatus — a type the provider could not collapse survives here
    public string? RawCancellationType { get; set; }

    /// <summary>Cancellation reason as worded by the marketplace.</summary>
    public string? CancelReason { get; set; }

    /// <summary>Deadline the marketplace expects the posting to be packed by.</summary>
    public DateTime? ShipmentDate { get; set; }

    public DateTime? InProcessAt { get; set; }
    public string? TrackingNumber { get; set; }
    public string? DeliveryMethodName { get; set; }

    /// <summary>How many packages the marketplace expects. A hint for the packer, not a box count in WMS.</summary>
    public int MultiBoxQty { get; set; } = 1;

    /// <summary>
    /// Barcode printed on the marketplace label, which is what ties a label page back to this posting.
    /// Ozon states it only while the posting is <c>awaiting_deliver</c> and blanks it afterwards, so once
    /// stored it is never cleared — a label reprinted later still has to be matched.
    /// </summary>
    public string? ScanitBarcode { get; set; }

    public Guid? LabelFileId { get; set; }
    public DataFile? LabelFile { get; set; }
    public DateTime? LabelFetchedAt { get; set; }

    [Column(TypeName = "jsonb")] public AppFieldError? LabelError { get; set; }

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
