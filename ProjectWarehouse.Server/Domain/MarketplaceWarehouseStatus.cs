namespace ProjectWarehouse.Server.Domain;

/// <summary>
/// Marketplace-agnostic warehouse availability. Providers collapse their own status vocabulary into
/// these three; anything unrecognised (or missing) becomes <see cref="Unavailable"/>, which is why it is 0.
/// </summary>
public enum MarketplaceWarehouseStatus
{
    Unavailable = 0,
    Active = 1,
    Inactive = 2,
}
