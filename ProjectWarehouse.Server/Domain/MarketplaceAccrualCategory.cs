namespace ProjectWarehouse.Server.Domain;

/// <summary>
/// What an accrual row pays for, collapsed from the marketplace's type ids. Unknown = 0 marks a type the
/// provider has no mapping for; the raw id is kept alongside.
/// </summary>
public enum MarketplaceAccrualCategory
{
    Unknown = 0,

    /// <summary>The seller's price of a sold item; negative when the sale is reversed.</summary>
    Sale = 1,

    /// <summary>The marketplace's cut of a sale; positive when the sale is reversed.</summary>
    Commission = 2,

    /// <summary>Delivery to the buyer, last mile included.</summary>
    Logistics = 3,

    /// <summary>Carrying a return, refusal or cancellation back, and accepting it.</summary>
    ReturnLogistics = 4,

    /// <summary>Drop-off, packing and other handling of a shipment.</summary>
    Processing = 5,

    Acquiring = 6,

    Advertising = 7,

    /// <summary>Storage and placement of stock at the marketplace.</summary>
    Storage = 8,

    Penalty = 9,

    Compensation = 10,

    /// <summary>Loyalty points and cashback the seller funds.</summary>
    Bonus = 11,

    /// <summary>Subscriptions, early payouts and other fees for the seller's own services.</summary>
    Services = 12,

    /// <summary>A mapped type that fits no other category.</summary>
    Other = 13,

    /// <summary>What the buyer paid for delivery the seller arranged, passed on to the seller.</summary>
    DeliveryCharge = 14,
}
