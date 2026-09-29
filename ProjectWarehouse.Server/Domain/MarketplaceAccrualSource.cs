namespace ProjectWarehouse.Server.Domain;

/// <summary>Which marketplace document an accrual row was read from.</summary>
public enum MarketplaceAccrualSource
{
    /// <summary>The finance accrual journal, day by day.</summary>
    AccrualJournal = 0,

    /// <summary>
    /// The report of items the marketplace bought out itself (Ozon: cross-border sales). Their sale never
    /// reaches the journal, only their fees do.
    /// </summary>
    BuyoutReport = 1,
}
