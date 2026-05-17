using ProjectWarehouse.Server.Infrastructure;

namespace ProjectWarehouse.Server.Models.Warehouses;

public class StoragePlaceNodeDto : IHasIdentity
{
    public Guid Id { get; init; }
    public string Name { get; init; } = null!;
    public Guid? ParentNodeId { get; init; }
    public int Order { get; init; }
    public int TotalItemsCount { get; init; }
}