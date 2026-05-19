using ProjectWarehouse.Server.Infrastructure;

namespace ProjectWarehouse.Server.Models.Warehouses;

public class StoragePlaceNodeDetailsDto : IHasIdentity
{
    public Guid Id { get; init; }
    public string Name { get; init; } = null!;
    public Guid StoragePlaceId { get; init; }
    public Guid? ParentNodeId { get; init; }
    public int Order { get; init; }
    public IReadOnlyList<ItemsGroupDto> ItemsGroups { get; init; } = [];
}
