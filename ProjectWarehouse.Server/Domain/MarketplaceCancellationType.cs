namespace ProjectWarehouse.Server.Domain;

/// <summary>
/// Who cancelled a posting, collapsed from the marketplace's own vocabulary. Unknown = 0 so an
/// unrecognized initiator never reads as a real one; the raw value is kept alongside for diagnosis.
/// </summary>
public enum MarketplaceCancellationType
{
    Unknown = 0,
    Seller = 1,
    Customer = 2,
    Marketplace = 3,
    System = 4,
    Delivery = 5,
}
