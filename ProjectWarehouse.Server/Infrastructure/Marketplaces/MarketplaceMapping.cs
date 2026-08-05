using ProjectWarehouse.Server.Domain;

namespace ProjectWarehouse.Server.Infrastructure.Marketplaces;

/// <summary>Which catalog items a marketplace card is allowed to point at. Shared by manual mapping and auto-mapping.</summary>
public static class MarketplaceMapping
{
    /// <summary>ProductGroup is excluded: it cannot be an order component, so it cannot back a card.</summary>
    public static readonly CatalogItemType[] MappableTypes =
        [CatalogItemType.Standard, CatalogItemType.Unit, CatalogItemType.Bundle, CatalogItemType.Variation];

    // Variation and Bundle carry no barcode by convention, but nothing in the schema enforces that —
    // so the barcode pass filters by type explicitly instead of trusting the column to be null.
    public static readonly CatalogItemType[] BarcodeMatchableTypes =
        [CatalogItemType.Standard, CatalogItemType.Unit];
}
