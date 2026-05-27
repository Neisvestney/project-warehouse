using ProjectWarehouse.Server.Models.Catalog;

namespace ProjectWarehouse.Server.Models.Inventory;

public class AssembledBundleInventoryItemDto
{
    public Guid Id { get; init; }
    public CatalogItemSummaryDto CatalogItem { get; init; } = null!;
    public Guid WarehouseId { get; init; }
    public string WarehouseName { get; init; } = null!;
    public Guid StoragePlaceId { get; init; }
    public string StoragePlaceName { get; init; } = null!;
    public Guid NodeId { get; init; }
    public string NodeName { get; init; } = null!;
}
