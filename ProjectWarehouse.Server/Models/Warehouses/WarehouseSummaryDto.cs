using ProjectWarehouse.Server.Infrastructure;

namespace ProjectWarehouse.Server.Models.Warehouses;

public class WarehouseSummaryDto : IHasIdentity
{
    public Guid Id { get; init; }
    public string Name { get; init; } = null!;
    public decimal Width { get; init; }
    public decimal Height { get; init; }
    public int StoragePlaceCount { get; init; }
    public int TotalItemsCount { get; init; }
}