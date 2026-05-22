using ProjectWarehouse.Server.Infrastructure;
using ProjectWarehouse.Server.Models.Warehouses;

namespace ProjectWarehouse.Server.Models.InboundOrderProcessing;

public class ProcessingStoragePlaceNodeDetailsDto: IHasIdentity
{
    public Guid Id { get; init; }
    public string Name { get; init; } = null!;
    public Guid StoragePlaceId { get; init; }
    public Guid? ParentNodeId { get; init; }
    public int Order { get; init; }
    public IReadOnlyList<ItemsGroupDto> ItemsGroups { get; init; } = [];
    public IReadOnlyList<ItemsGroupDto> OrderItemsGroups { get; init; } = [];
}
