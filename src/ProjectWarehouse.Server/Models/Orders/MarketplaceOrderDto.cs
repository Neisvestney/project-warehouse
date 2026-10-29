using ProjectWarehouse.Server.Domain;

namespace ProjectWarehouse.Server.Models.Orders;

/// <summary>
/// Marketplace side of an order, inlined into the order DTOs — the entity has no page of its own.
/// </summary>
public class MarketplaceOrderDto
{
    public Guid MarketplaceAccountId { get; init; }
    public string MarketplaceAccountName { get; init; } = null!;
    public MarketplaceType MarketplaceType { get; init; }

    public string PostingNumber { get; init; } = null!;
    public string? ExternalOrderNumber { get; init; }

    /// <summary>Rebuilt from the finance report: status and price are approximate.</summary>
    public bool IsReconstructed { get; init; }

    public MarketplaceOrderStatus Status { get; init; }

    /// <summary>When a sync first saw the posting delivered, to within the sync interval; null if imported delivered.</summary>
    public DateTime? DeliveredAt { get; init; }

    /// <summary>Same as <see cref="DeliveredAt"/>, for cancelled.</summary>
    public DateTime? CancelledAt { get; init; }
    public string? RawStatus { get; init; }
    public string? RawSubstatus { get; init; }

    /// <summary>Null while the posting is alive; <c>true</c> when it was cancelled after being packed.</summary>
    public bool? CancelledAfterAssembly { get; init; }

    public MarketplaceCancellationType? CancellationType { get; init; }
    public string? RawCancellationType { get; init; }
    public string? CancelReason { get; init; }
    public MarketplaceLossReason? CancelReasonCategory { get; init; }

    /// <summary>Whether the buyer sent the order back, fully or in part.</summary>
    public MarketplaceOrderReturnState ReturnState { get; init; }

    public DateTime? ShipmentDate { get; init; }
    public DateTime? InProcessAt { get; init; }

    /// <summary>When the posting was handed over to delivery; set on a cancelled one that had left the warehouse.</summary>
    public DateTime? DeliveringAt { get; init; }

    public DateTime? ShipmentDateWithoutDelay { get; init; }
    public DateTime? DeliveryDateBegin { get; init; }
    public DateTime? DeliveryDateEnd { get; init; }

    public string? TrackingNumber { get; init; }
    public string? DeliveryMethodName { get; init; }
    public int MultiBoxQty { get; init; }

    /// <summary>Number printed on the marketplace label: Ozon scanit or Wildberries sticker number. Null until the label exists.</summary>
    public string? LabelBarcode { get; init; }

    /// <summary>The supply the marketplace put the posting in; null where there are no supplies or it is in none yet.</summary>
    public MarketplaceSupplyDto? Supply { get; init; }

    public DateTime StatusSyncedAt { get; init; }
}
