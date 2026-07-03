using ProjectWarehouse.Server.Infrastructure;

namespace ProjectWarehouse.Server.Domain;

/// <summary>
/// Records what was physically picked from inventory for one task box component.
/// Exactly one fulfillment scenario applies:
/// - Standard:           SourceNodeId + Quantity
/// - Unit:               UnitInventoryItemId
/// - AssembledBundle:    AssembledBundleInventoryItemId
/// - Bundle mode 2:      AssembledBundleInventoryItemId (same path as AssembledBundle)
/// - Bundle mode 1:      BundleComponents[] (each component has its own SourceNodeId)
/// </summary>
public class AssemblyFulfillment : IHasIdentity
{
    public Guid Id { get; set; }

    public Guid TaskBoxComponentId { get; set; }
    public AssemblyTaskBoxComponent TaskBoxComponent { get; set; } = null!;

    /// <summary>Null for Bundle mode 1; each bundle component carries its own source node.</summary>
    public Guid? SourceNodeId { get; set; }
    public StoragePlaceNode? SourceNode { get; set; }

    // Standard
    public int Quantity { get; set; }

    // Unit — UnitInventoryItemId goes null (SetNull) after item is deleted; number stored for restoration
    public Guid? UnitInventoryItemId { get; set; }
    public UnitInventoryItem? UnitInventoryItem { get; set; }
    public string? UnitInventoryNumber { get; set; }

    // AssembledBundle or Bundle mode 2
    // AssembledBundleInventoryItemId goes null (SetNull) after item is deleted; components stored for restoration
    public Guid? AssembledBundleInventoryItemId { get; set; }
    public AssembledBundleInventoryItem? AssembledBundleInventoryItem { get; set; }
    public ICollection<AssemblyFulfillmentAssembledBundleComponentSnapshot> AssembledBundleComponentSnapshots { get; set; } = [];

    // Bundle mode 1
    public ICollection<AssemblyFulfillmentBundleComponent> BundleComponents { get; set; } = [];
}
