namespace ProjectWarehouse.Server.Models.Warehouses;

public class WarehouseDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = null!;
    public int Width { get; init; }
    public int Height { get; init; }
    public IReadOnlyList<StoragePlaceDto> StoragePlaces { get; init; } = [];
}