using ProjectWarehouse.Server.Infrastructure;

namespace ProjectWarehouse.Server.Domain;

/// <summary>
/// Records what was physically picked from inventory for one task box component.
/// Exactly one fulfillment scenario applies:
/// - Standard: SourceNodeId + Quantity
/// - Unit:     UnitInventoryItemId
/// - Bundle:   BundleComponents[] (each component has its own SourceNodeId)
/// </summary>
public class AssemblyFulfillment : IHasIdentity
{
    public Guid Id { get; set; }

    public Guid TaskBoxComponentId { get; set; }
    public AssemblyTaskBoxComponent TaskBoxComponent { get; set; } = null!;

    /// <summary>Null for Bundle; each bundle component carries its own source node.</summary>
    public Guid? SourceNodeId { get; set; }
    public StoragePlaceNode? SourceNode { get; set; }

    // Standard
    public int Quantity { get; set; }

    // Unit — UnitInventoryItemId goes null (SetNull) after item is deleted; number stored for restoration
    public Guid? UnitInventoryItemId { get; set; }
    public UnitInventoryItem? UnitInventoryItem { get; set; }
    public string? UnitInventoryNumber { get; set; }

    // Bundle
    public ICollection<AssemblyFulfillmentBundleComponent> BundleComponents { get; set; } = [];
}
