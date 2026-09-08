namespace ProjectWarehouse.Server.Models.Warehouses;

public class NodeItemCountDto
{
    public Guid NodeId { get; init; }
    public Guid CatalogItemId { get; init; }
    public int Count { get; init; }
}
