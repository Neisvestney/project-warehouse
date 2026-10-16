namespace ProjectWarehouse.Server.Domain;

/// <summary>Fulfillment scheme of the posting a return belongs to. Unknown = 0, same rule as the other raw vocabularies.</summary>
public enum MarketplaceReturnScheme
{
    Unknown = 0,
    Fbs = 1,
    Fbo = 2,
}
