namespace ProjectWarehouse.Server.Domain;

/// <summary>
/// Values are pinned — they are stored as int and referenced from jsonb snapshots.
/// </summary>
public enum OrderType
{
    /// <summary>Seller-fulfilled posting: WMS assembles it and hands it to the carrier.</summary>
    FBS = 0,

    /// <summary>A supply shipped to the marketplace's own warehouse — boxes are packed here and handed over.</summary>
    FboSupply = 1,

    Direct = 2,

    /// <summary>
    /// A posting the marketplace ships to the customer from its own stock. WMS never touches the goods,
    /// so these are always imported as <see cref="Order.IsExternal"/>.
    /// </summary>
    FboPosting = 3,
}
