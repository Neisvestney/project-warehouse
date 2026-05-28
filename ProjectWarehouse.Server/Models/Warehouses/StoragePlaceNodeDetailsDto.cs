using ProjectWarehouse.Server.Infrastructure;

namespace ProjectWarehouse.Server.Models.Warehouses;

public class StoragePlaceNodeDetailsDto : IHasIdentity
{
    public Guid Id { get; init; }
    public string Name { get; init; } = null!;
    public Guid StoragePlaceId { get; init; }
    public Guid? ParentNodeId { get; init; }
    public int Order { get; init; }

    /// <summary>Current standard items stored in this node. Used for inventory changelog diffing.</summary>
    public IReadOnlyList<ItemsGroupDto> ItemsGroups { get; init; } = [];

    /// <summary>Count of unit inventory items at this node. Used for inventory changelog diffing.</summary>
    public int UnitItemsCount { get; init; }

    /// <summary>Count of assembled bundle inventory items at this node. Used for inventory changelog diffing.</summary>
    public int AssembledBundlesCount { get; init; }
}
