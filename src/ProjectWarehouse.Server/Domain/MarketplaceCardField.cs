namespace ProjectWarehouse.Server.Domain;

/// <summary>Card field an auto-mapping rule matches against. <see cref="Barcode"/> matches if any barcode does.</summary>
public enum MarketplaceCardField
{
    OfferId = 0,
    Sku = 1,
    ExternalId = 2,
    Name = 3,
    Barcode = 4,
}
