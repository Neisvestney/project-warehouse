using ProjectWarehouse.Server.Models.Catalog;

namespace ProjectWarehouse.Server.Models.Inventory;

public class InventoryItemSummaryDto
{
    public Guid CatalogItemId { get; init; }
    public CatalogItemSummaryDto CatalogItem { get; init; } = null!;
    public int Count { get; init; }
}
