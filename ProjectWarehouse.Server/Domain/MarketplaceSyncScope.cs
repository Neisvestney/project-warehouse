namespace ProjectWarehouse.Server.Domain;

public enum MarketplaceSyncScope
{
    Warehouses = 0,
    Cards = 1,

    /// <summary>
    /// Importing postings. Manual only, and deliberately outside <see cref="All"/>: warehouses and
    /// cards are happy on a background interval, new orders are wanted right now or not at all.
    /// Runs <see cref="OrdersBackground"/> as its second phase.
    /// </summary>
    Orders = 3,

    /// <summary>
    /// Everything about orders that needs no operator watching it: statuses of the postings already
    /// imported, and — once it lands — the FBO import. It creates nothing an unmapped catalogue could
    /// silently skip, which is what kept <see cref="Orders"/> out of <see cref="All"/>.
    /// </summary>
    OrdersBackground = 4,

    All = 2,
}
