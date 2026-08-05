namespace ProjectWarehouse.Server.Domain;

public enum AppEntityType
{
    Unknown,
    User,
    Roles,
    Warehouse,
    CatalogItem,
    StoragePlaceNode,
    Receipt,
    Writeoff,
    Order,

    // Persisted as int — append only, never reorder.
    MarketplaceAccount,
    MarketplaceCard,
}