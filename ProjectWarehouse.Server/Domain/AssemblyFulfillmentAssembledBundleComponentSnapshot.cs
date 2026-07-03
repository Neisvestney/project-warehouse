using ProjectWarehouse.Server.Infrastructure;

namespace ProjectWarehouse.Server.Domain;

/// <summary>
/// Snapshots an AssembledBundleInventoryItem's component before it is removed as part of a fulfillment.
/// Used to recreate the bundle when the fulfillment is rolled back.
/// Either UnitInventoryItemId (Unit component) or CatalogItemId + Quantity (Standard component) is set.
/// </summary>
public class AssemblyFulfillmentAssembledBundleComponentSnapshot : IHasIdentity
{
    public Guid Id { get; set; }

    public Guid FulfillmentId { get; set; }
    public AssemblyFulfillment Fulfillment { get; set; } = null!;

    // Unit component — unit item still exists after bundle removal (SetNull cascade doesn't delete it)
    public Guid? UnitInventoryItemId { get; set; }
    public UnitInventoryItem? UnitInventoryItem { get; set; }

    // Standard component
    public Guid? CatalogItemId { get; set; }
    public CatalogItem? CatalogItem { get; set; }
    public int? Quantity { get; set; }
}
