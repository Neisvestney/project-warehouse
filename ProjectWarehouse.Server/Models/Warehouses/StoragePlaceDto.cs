namespace ProjectWarehouse.Server.Models.Warehouses;

public class StoragePlaceDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = null!;
    public decimal X { get; init; }
    public decimal Y { get; init; }
    public decimal Width { get; init; }
    public decimal Height { get; init; }
    public int TotalItemsCount { get; init; }
}