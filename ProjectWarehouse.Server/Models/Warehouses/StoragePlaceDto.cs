namespace ProjectWarehouse.Server.Models.Warehouses;

public class StoragePlaceDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = null!;
    public int X { get; init; }
    public int Y { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
}