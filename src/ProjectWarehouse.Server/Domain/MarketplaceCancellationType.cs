namespace ProjectWarehouse.Server.Domain;

public enum MarketplaceCancellationType
{
    // The source supplied an initiator the mapping does not recognize.
    Unknown = 0,
    // The source does not report the cancellation initiator.
    NoData = 6,
    Seller = 1,
    Customer = 2,
    Marketplace = 3,
    System = 4,
    Delivery = 5,
}
