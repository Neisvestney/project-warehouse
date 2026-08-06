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
    MarketplaceAccount,
    MarketplaceCard,

    /// <summary>
    /// The changelog itself. Not something a user opens a page for — it exists so the storage
    /// statistics can name the table instead of lumping it into Unknown.
    /// </summary>
    ChangeLog,

    /// <summary>Stock rows. Like <see cref="ChangeLog"/>, present for statistics rather than for a page.</summary>
    InventoryItem,
}