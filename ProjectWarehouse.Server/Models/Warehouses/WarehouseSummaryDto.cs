namespace ProjectWarehouse.Server.Models.Warehouses;

public class WarehouseSummaryDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = null!;
    public int Width { get; init; }
    public int Height { get; init; }
    public int StoragePlaceCount { get; init; }
}