namespace ProjectWarehouse.Server.Models.Warehouses;

public class ItemsGroupDto
{
    public Guid Id { get; init; }
    public int Count { get; init; }
    public NodeCharacteristicDto CatalogItemWithCharacteristic { get; init; } = null!;
}
