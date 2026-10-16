using ProjectWarehouse.Server.Domain;

namespace ProjectWarehouse.Server.Models.Orders;

public class AssemblyFulfillmentBundleComponentDto
{
    public Guid Id { get; init; }
    public Guid CatalogItemId { get; init; }
    public string CatalogItemName { get; init; } = null!;
    public CatalogItemType CatalogItemType { get; init; }
    public Guid SourceNodeId { get; init; }
    public string[] SourceNodePath { get; init; } = [];
    public int Quantity { get; init; }
    public Guid? UnitInventoryItemId { get; init; }
    public string? UnitInventoryNumber { get; init; }
}
