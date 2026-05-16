using ProjectWarehouse.Server.Infrastructure;

namespace ProjectWarehouse.Server.Models.Warehouses;

public class ItemsGroupDto : IHasIdentity
{
    public Guid Id { get; init; }
    public int Count { get; init; }
    public NodeCharacteristicDto CatalogItemWithCharacteristic { get; init; } = null!;
}
