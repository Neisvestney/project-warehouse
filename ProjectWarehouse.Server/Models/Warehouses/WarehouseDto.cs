using ProjectWarehouse.Server.Infrastructure;

namespace ProjectWarehouse.Server.Models.Warehouses;

public class WarehouseDto : IHasIdentity
{
    public Guid Id { get; init; }
    public string Name { get; init; } = null!;
    public decimal Width { get; init; }
    public decimal Height { get; init; }
    public IReadOnlyList<StoragePlaceDto> StoragePlaces { get; init; } = [];
    public IReadOnlyList<WarehouseLayoutElementDto> LayoutObjects { get; init; } = [];
    public int TotalItemsCount { get; init; }
}