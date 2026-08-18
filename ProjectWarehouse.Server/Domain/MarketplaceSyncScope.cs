namespace ProjectWarehouse.Server.Domain;

public enum MarketplaceSyncScope
{
    Warehouses = 0,
    Cards = 1,

    /// <summary>
    /// Manual only, and deliberately outside <see cref="All"/>: warehouses and cards are happy on a
    /// background interval, orders are wanted right now or not at all.
    /// </summary>
    Orders = 3,

    All = 2,
}
