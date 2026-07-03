namespace ProjectWarehouse.Server.Models.Orders;

/// <summary>
/// Exactly one fulfillment scenario must be provided:
/// - Standard:        SourceNodeId + Quantity > 0
/// - Unit:            UnitInventoryItemId + SourceNodeId
/// - AssembledBundle: AssembledBundleInventoryItemId + SourceNodeId
/// - Bundle mode 2:   AssembledBundleInventoryItemId + SourceNodeId (same as AssembledBundle)
/// - Bundle mode 1:   BundleComponents (non-empty); SourceNodeId is ignored
/// </summary>
public class AddFulfillmentRequest
{
    public Guid? SourceNodeId { get; init; }

    // Standard
    public int Quantity { get; init; }

    // Unit
    public Guid? UnitInventoryItemId { get; init; }

    // AssembledBundle or Bundle mode 2
    public Guid? AssembledBundleInventoryItemId { get; init; }

    // Bundle mode 1
    public IReadOnlyList<AddFulfillmentBundleComponentRequest>? BundleComponents { get; init; }
}
