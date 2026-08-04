using ProjectWarehouse.Server.Domain;

namespace ProjectWarehouse.Server.Models.Orders;

public class AssemblyFulfillmentDto
{
    public Guid Id { get; init; }
    public Guid? SourceNodeId { get; init; }
    public string[] SourceNodePath { get; init; } = [];

    // Standard
    public int Quantity { get; init; }

    // Unit
    public Guid? UnitInventoryItemId { get; init; }
    public string? UnitInventoryNumber { get; init; }

    // Bundle
    public IReadOnlyList<AssemblyFulfillmentBundleComponentDto> BundleComponents { get; init; } = [];

    // Which item was actually picked — set for Variation components; null for pre-migration rows.
    public Guid? ResolvedCatalogItemId { get; init; }
    public string? ResolvedCatalogItemName { get; init; }
    public CatalogItemType? ResolvedCatalogItemType { get; init; }

    public DateTime CreatedAt { get; init; }
    public string? CreatedByName { get; init; }
}
