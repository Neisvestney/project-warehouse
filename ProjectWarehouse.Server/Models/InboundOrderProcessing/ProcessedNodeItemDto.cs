using ProjectWarehouse.Server.Models.Warehouses;

namespace ProjectWarehouse.Server.Models.InboundOrderProcessing;

public class ProcessedNodeItemDto
{
    public NodeCharacteristicDto CatalogItemWithCharacteristic { get; init; } = null!;
    public int Count { get; init; }
}
