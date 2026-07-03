namespace ProjectWarehouse.Server.Models.Orders;

public class AssemblyFulfillmentDto
{
    public Guid Id { get; init; }
    public Guid? SourceNodeId { get; init; }

    // Standard
    public int Quantity { get; init; }

    // Unit
    public Guid? UnitInventoryItemId { get; init; }

    // AssembledBundle or Bundle mode 2
    public Guid? AssembledBundleInventoryItemId { get; init; }

    // Bundle mode 1
    public IReadOnlyList<AssemblyFulfillmentBundleComponentDto> BundleComponents { get; init; } = [];
}
