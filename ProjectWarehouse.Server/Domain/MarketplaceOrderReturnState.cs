namespace ProjectWarehouse.Server.Domain;

/// <summary>How much of a sold posting came back, counted in units.</summary>
public enum MarketplaceOrderReturnState
{
    None = 0,
    Partial = 1,
    Full = 2,
}
