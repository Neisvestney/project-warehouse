namespace ProjectWarehouse.Server.Models.Warehouses;

public class StoragePlaceNodeDetailsDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = null!;
    public Guid? ParentNodeId { get; init; }
    public IReadOnlyList<ItemsGroupDto> ItemsGroups { get; init; } = [];
}
