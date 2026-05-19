using ProjectWarehouse.Server.Models.Warehouses;

namespace ProjectWarehouse.Server.Models.InboundOrders;

public class InboundOrderItemsComparisonDto
{
    public IReadOnlyList<ComparisonItemDto> DeclaredItems { get; init; } = [];
    public IReadOnlyList<ComparisonItemDto> ProcessedItems { get; init; } = [];
    public IReadOnlyList<ItemDifferenceDto> Shortages { get; init; } = [];
    public IReadOnlyList<ItemDifferenceDto> Surpluses { get; init; } = [];
    public int TotalShortageCount { get; init; }
    public int TotalSurplusCount { get; init; }
}

public class ComparisonItemDto
{
    public NodeCharacteristicDto CatalogItemWithCharacteristic { get; init; } = null!;
    public int Count { get; init; }
}

public class ItemDifferenceDto
{
    public NodeCharacteristicDto CatalogItemWithCharacteristic { get; init; } = null!;
    public int DeclaredCount { get; init; }
    public int ProcessedCount { get; init; }
    public int DifferenceCount { get; init; }
}
