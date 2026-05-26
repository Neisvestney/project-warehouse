using ProjectWarehouse.Server.Infrastructure;

namespace ProjectWarehouse.Server.Domain;

public abstract class InventoryItem : IHasIdentity
{
    public Guid Id { get; set; }

    public Guid CatalogItemId { get; set; }
    public CatalogItem CatalogItem { get; set; } = null!;

    public Guid StoragePlaceNodeId { get; set; }
    public StoragePlaceNode StoragePlaceNode { get; set; } = null!;
}
