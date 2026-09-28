namespace ProjectWarehouse.Server.Domain;

/// <summary>What an accrual is about. Unknown = 0 so an unrecognized kind never reads as a real one.</summary>
public enum MarketplaceAccrualScope
{
    Unknown = 0,

    /// <summary>A posting: its sale, commission and delivery. <see cref="MarketplaceAccrual.UnitNumber"/> is the posting.</summary>
    Posting = 1,

    /// <summary>A product of a posting or of the stock: acquiring, packing, placement.</summary>
    Item = 2,

    /// <summary>The shop as a whole: advertising, storage, penalties, compensations. Linked to no order.</summary>
    Account = 3,

    /// <summary>A supply container.</summary>
    Container = 4,
}
