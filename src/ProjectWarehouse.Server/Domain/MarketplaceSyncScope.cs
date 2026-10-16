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

    /// <summary>
    /// One-off history import over an administrator-chosen period, FBS and FBO alike. Manual only and
    /// outside <see cref="All"/>: it creates nothing but external orders, and only for postings the
    /// marketplace has already finished with. Its period lives on the run row, not in the queue.
    /// </summary>
    OrdersBackfill = 5,

    /// <summary>
    /// The seller's accruals — what the marketplace paid and kept, day by day. Part of <see cref="All"/>: it
    /// creates nothing but its own rows. Kept out of <see cref="OrdersBackground"/> so a slow or throttled
    /// finance API cannot fail the order refresh. <see cref="OrdersBackfill"/> reads the same history for its period.
    /// </summary>
    Accruals = 6,

    All = 2,
}
