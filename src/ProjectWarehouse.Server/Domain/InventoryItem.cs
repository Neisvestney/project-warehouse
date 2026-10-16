using ProjectWarehouse.Server.Infrastructure;

namespace ProjectWarehouse.Server.Domain;

public abstract class InventoryItem : IHasIdentity
{
    public Guid Id { get; set; }

    public Guid CatalogItemId { get; set; }
    public CatalogItem CatalogItem { get; set; } = null!;

    /// <summary>Null while the item is detached — held by an assembly fulfillment, not in any physical location.</summary>
    public Guid? StoragePlaceNodeId { get; set; }
    public StoragePlaceNode? StoragePlaceNode { get; set; }
}
