using ProjectWarehouse.Server.Infrastructure;

namespace ProjectWarehouse.Server.Domain;

/// <summary>
/// Represents a single line in a write-off document. Exactly one of the three scenarios applies:
/// - Standard item:         CatalogItemId != null, Count > 0, UnitInventoryItemId == null, AssembledBundleInventoryItemId == null
/// - Unit item:             CatalogItemId == null, Count == 0, UnitInventoryItemId != null
/// - Assembled bundle item: CatalogItemId == null, Count == 0, AssembledBundleInventoryItemId != null
/// Each item carries its own source storage node.
/// </summary>
public class WriteoffItem : IHasIdentity
{
    public Guid Id { get; set; }

    public Guid WriteoffId { get; set; }
    public Writeoff Writeoff { get; set; } = null!;

    /// <summary>The storage node from which this item will be removed.</summary>
    public Guid SourceNodeId { get; set; }
    public StoragePlaceNode SourceNode { get; set; } = null!;

    public string? Notes { get; set; }

    // Standard item fields
    public Guid? CatalogItemId { get; set; }
    public CatalogItem? CatalogItem { get; set; }
    public int Count { get; set; }

    // Unit item fields
    public Guid? UnitInventoryItemId { get; set; }
    public UnitInventoryItem? UnitInventoryItem { get; set; }

    // Assembled bundle fields
    public Guid? AssembledBundleInventoryItemId { get; set; }
    public AssembledBundleInventoryItem? AssembledBundleInventoryItem { get; set; }
}
