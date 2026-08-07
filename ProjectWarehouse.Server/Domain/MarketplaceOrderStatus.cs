namespace ProjectWarehouse.Server.Domain;

/// <summary>
/// Normalized posting state. Collapsing the marketplace's own vocabulary is the provider's job.
/// Unknown = 0 for the same reason as MarketplaceWarehouseStatus.Unavailable: an unrecognized state
/// must not look like a working one.
/// </summary>
public enum MarketplaceOrderStatus
{
    Unknown = 0,
    AwaitingDeliver = 1,
    Delivering = 2,
    Delivered = 3,
    Cancelled = 4,
    Arbitration = 5,
}
