namespace ProjectWarehouse.Server.Domain;

/// <summary>
/// What sent an item back, collapsed from the marketplace's own vocabulary. Unknown = 0 so an
/// unrecognized type never reads as a real one; the raw value is kept alongside for diagnosis.
/// </summary>
public enum MarketplaceReturnKind
{
    Unknown = 0,

    /// <summary>Cancelled after shipment, refusal on delivery included; the posting itself ends up cancelled.</summary>
    Cancellation = 1,

    FullRefusal = 2,

    /// <summary>Part of a posting refused on delivery; the posting itself counts as delivered.</summary>
    PartialRefusal = 3,

    /// <summary>Returned by the customer after delivery.</summary>
    CustomerReturn = 4,
}
