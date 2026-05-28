using ProjectWarehouse.Server.Infrastructure;

namespace ProjectWarehouse.Server.Domain;

/// <summary>
/// Tracks the physical placement of receipt items into storage nodes.
/// Exactly one of the three scenarios applies:
/// - Standard item:          Count > 0, UnitInventoryItemId == null, AssembledBundleInventoryItemId == null
/// - Unit item:              Count == 0, UnitInventoryItemId != null
/// - Assembled bundle item:  Count == 0, AssembledBundleInventoryItemId != null
/// </summary>
public class ReceiptItemPlacement : IHasIdentity
{
    public Guid Id { get; set; }

    public Guid ReceiptItemId { get; set; }
    public ReceiptItem ReceiptItem { get; set; } = null!;

    public Guid StoragePlaceNodeId { get; set; }
    public StoragePlaceNode StoragePlaceNode { get; set; } = null!;

    /// <summary>Quantity placed. Used for Standard (count-based) catalog items. Zero for Unit/Bundle placements.</summary>
    public int Count { get; set; }

    /// <summary>Reference to the created UnitInventoryItem. Set when placing a Unit catalog item.</summary>
    public Guid? UnitInventoryItemId { get; set; }
    public UnitInventoryItem? UnitInventoryItem { get; set; }

    /// <summary>Reference to the created AssembledBundleInventoryItem. Set when placing an AssembledBundle catalog item.</summary>
    public Guid? AssembledBundleInventoryItemId { get; set; }
    public AssembledBundleInventoryItem? AssembledBundleInventoryItem { get; set; }
}
