namespace ProjectWarehouse.Server.Models.Warehouses;

public class StoragePlaceNodeDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = null!;
    public Guid? ParentNodeId { get; init; }
}