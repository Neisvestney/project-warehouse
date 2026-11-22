namespace ProjectWarehouse.Server.Models.Orders;

public enum OrderSortBy
{
    Number = 0,
    Status = 1,
    CreatedAt = 2,
    PlannedShipmentAt = 3,
    WarehouseName = 4,
    AssembledAt = 5,
    ShippedAt = 6,
    LabelFetchedAt = 7,
    PositionCount = 8,
    Composition = 9,
    MarketplaceType = 10,
    MarketplaceAccountName = 11,
    ShipmentDate = 12,
    StatusSyncedAt = 13,
    ReturnState = 14,
    CancelReason = 15,
    Notes = 16,
    MarketplaceStatus = 17,
    PostingNumber = 18,
    Supply = 19,
    ComponentCount = 20,
}
