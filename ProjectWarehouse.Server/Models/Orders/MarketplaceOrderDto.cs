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

    public MarketplaceOrderStatus Status { get; init; }
    public string? RawStatus { get; init; }
    public string? RawSubstatus { get; init; }

    /// <summary>Null while the posting is alive; <c>true</c> when it was cancelled after being shipped.</summary>
    public bool? CancelledAfterShip { get; init; }

    public MarketplaceCancellationType? CancellationType { get; init; }
    public string? RawCancellationType { get; init; }
    public string? CancelReason { get; init; }

    public DateTime? ShipmentDate { get; init; }
    public DateTime? InProcessAt { get; init; }
    public string? TrackingNumber { get; init; }
    public string? DeliveryMethodName { get; init; }
    public int MultiBoxQty { get; init; }

    /// <summary>Set once the label has been printed; downloadable through /api/files/{id}/content.</summary>
    public Guid? LabelFileId { get; init; }
    public DateTime? LabelFetchedAt { get; init; }
    public AppFieldError? LabelError { get; init; }

    public DateTime StatusSyncedAt { get; init; }
}
