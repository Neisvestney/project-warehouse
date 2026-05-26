using ProjectWarehouse.Server.Infrastructure;

namespace ProjectWarehouse.Server.Domain;

// Invariant: either UnitInventoryItemId is set (Unit component)
// or both CatalogItemId and Quantity are set (Standard component).
public class AssembledBundleInventoryItemComponent : IHasIdentity
{
    public Guid Id { get; set; }

    public Guid AssembledBundleInventoryItemId { get; set; }
    public AssembledBundleInventoryItem AssembledBundleInventoryItem { get; set; } = null!;

    public Guid? UnitInventoryItemId { get; set; }
    public UnitInventoryItem? UnitInventoryItem { get; set; }

    public Guid? CatalogItemId { get; set; }
    public CatalogItem? CatalogItem { get; set; }

    public int? Quantity { get; set; }
}
