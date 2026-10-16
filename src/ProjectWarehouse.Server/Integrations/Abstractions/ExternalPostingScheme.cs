namespace ProjectWarehouse.Server.Integrations.Abstractions;

/// <summary>
/// Which fulfillment scheme a posting belongs to. The two carry different payloads and turn into
/// different <see cref="Domain.OrderType"/>s, so the mapping cannot tell them apart without it.
/// </summary>
public enum ExternalPostingScheme
{
    /// <summary>Seller-fulfilled: the goods sit in a WMS warehouse.</summary>
    Fbs = 0,

    /// <summary>Marketplace-fulfilled: the goods sit at the marketplace and WMS only records the sale.</summary>
    Fbo = 1,
}
