namespace ProjectWarehouse.Server.Domain;

/// <summary>
/// Normalized posting state. Collapsing the marketplace's own vocabulary is the provider's job.
/// Unknown = 0 for the same reason as MarketplaceWarehouseStatus.Unavailable: an unrecognized state
/// must not look like a working one.
/// </summary>
public enum MarketplaceOrderStatus
{
    Unknown = 0,

    /// <summary>Handed to the seller, not packed on the marketplace yet: composition may change, no label.</summary>
    AwaitingPackaging = 6,

    AwaitingDeliver = 1,
    Delivering = 2,
    Delivered = 3,
    Cancelled = 4,
    Arbitration = 5,

    /// <summary>Split into other postings and gone itself. Final, and never a cancellation.</summary>
    Split = 7,
}
