namespace ProjectWarehouse.Server.Models.Orders;

/// <summary>
/// Exactly one fulfillment scenario must be provided:
/// - Standard: SourceNodeId + Quantity > 0
/// - Unit:     UnitInventoryItemId + SourceNodeId
/// - Bundle:   BundleComponents (non-empty); SourceNodeId is ignored
/// </summary>
public class AddFulfillmentRequest
{
    public Guid? SourceNodeId { get; init; }

    // Standard
    public int Quantity { get; init; }

    // Unit
    public Guid? UnitInventoryItemId { get; init; }

    // Bundle
    public IReadOnlyList<AddFulfillmentBundleComponentRequest>? BundleComponents { get; init; }

    /// <summary>
    /// For a Variation component — the member actually picked. Required for Standard and Bundle
    /// scenarios; derived server-side from the unit item for the Unit scenario. Ignored otherwise.
    /// </summary>
    public Guid? ResolvedCatalogItemId { get; init; }
}
