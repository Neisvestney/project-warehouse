namespace ProjectWarehouse.Server.Domain;

public enum AppEntityType
{
    Unknown = 0,
    User = 1,
    Roles = 2,
    Warehouse = 3,
    CatalogItem = 4,
    StoragePlaceNode = 5,
    Receipt = 6,
    Writeoff = 7,
    Order = 8,
    Stocktake = 14,
    MarketplaceAccount = 9,
    MarketplaceCard = 10,

    /// <summary>
    /// The changelog itself. Not something a user opens a page for — it exists so the storage
    /// statistics can name the table instead of lumping it into Unknown.
    /// </summary>
    ChangeLog = 11,

    /// <summary>Stock rows. Like <see cref="ChangeLog"/>, present for statistics rather than for a page.</summary>
    InventoryItem = 12,

    /// <summary>The stock movement journal. Present for storage statistics, nothing writes a changelog entry with it.</summary>
    StockMovement = 13,
}