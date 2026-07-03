namespace ProjectWarehouse.Server.Models.Orders;

public class AssemblyFulfillmentBundleComponentDto
{
    public Guid Id { get; init; }
    public Guid CatalogItemId { get; init; }
    public string CatalogItemName { get; init; } = null!;
    public Guid SourceNodeId { get; init; }
    public int Quantity { get; init; }
    public Guid? UnitInventoryItemId { get; init; }
}
