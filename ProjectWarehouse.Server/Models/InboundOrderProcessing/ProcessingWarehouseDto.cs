using ProjectWarehouse.Server.Infrastructure;
using ProjectWarehouse.Server.Models.Warehouses;

namespace ProjectWarehouse.Server.Models.InboundOrderProcessing;

public class ProcessingWarehouseDto : IHasIdentity
{
    public Guid Id { get; init; }
    public string Name { get; init; } = null!;
    public decimal Width { get; init; }
    public decimal Height { get; init; }
    public IReadOnlyList<ProcessingStoragePlaceDto> StoragePlaces { get; init; } = [];
    public IReadOnlyList<WarehouseLayoutElementDto> LayoutObjects { get; init; } = [];
}
