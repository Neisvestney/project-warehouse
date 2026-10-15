namespace ProjectWarehouse.Server.Domain;

/// <summary>
/// Why units were lost to a cancellation or a return, collapsed from each marketplace's own vocabulary into one
/// list shared by both. Unknown = 0: the marketplace gave a reason the provider could not recognize; its wording
/// stays alongside. Values come in blocks of twenty by whose side the reason falls on — buyer, seller,
/// marketplace and its delivery.
/// </summary>
public enum MarketplaceLossReason
{
    Unknown = 0,

    BuyerChangedMind = 1,
    BuyerFoundCheaper = 2,
    BuyerDeliveryTerms = 3,
    BuyerDidNotFit = 4,
    BuyerNotPickedUp = 5,
    BuyerOther = 6,

    OutOfStock = 20,
    NotShippedInTime = 21,
    SellerCancelled = 22,
    Defect = 23,
    PoorQuality = 24,
    WrongItem = 25,
    Incomplete = 26,
    CardMismatch = 27,
    Counterfeit = 28,

    DeliveryFailed = 40,
    DeliveryDelayed = 41,
    DamagedInDelivery = 42,
    MarketplaceFailure = 43,
    MarketplaceOther = 44,
}
