using ProjectWarehouse.Server.Infrastructure;

namespace ProjectWarehouse.Server.Domain;

/// <summary>
/// One component within a Bundle mode 1 fulfillment.
/// Exactly one scenario: Standard (Quantity) or Unit (UnitInventoryItemId).
/// </summary>
public class AssemblyFulfillmentBundleComponent : IHasIdentity
{
    public Guid Id { get; set; }

    public Guid FulfillmentId { get; set; }
    public AssemblyFulfillment Fulfillment { get; set; } = null!;

    public Guid CatalogItemId { get; set; }
    public CatalogItem CatalogItem { get; set; } = null!;

    public Guid SourceNodeId { get; set; }
    public StoragePlaceNode SourceNode { get; set; } = null!;

    // Standard component
    public int Quantity { get; set; }

    // Unit component — UnitInventoryItemId goes null (SetNull) after item is deleted; number stored for restoration
    public Guid? UnitInventoryItemId { get; set; }
    public UnitInventoryItem? UnitInventoryItem { get; set; }
    public string? UnitInventoryNumber { get; set; }
}
