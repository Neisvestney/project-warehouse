using ProjectWarehouse.Server.Infrastructure;

namespace ProjectWarehouse.Server.Models.InboundOrderProcessing;

public class ProcessingStoragePlaceNodeDto: IHasIdentity
{
    public Guid Id { get; init; }
    public string Name { get; init; } = null!;
    public Guid? ParentNodeId { get; init; }
    public int Order { get; init; }
    public int TotalItemsCount { get; init; }
    public bool HasOrderItems { get; init; }
}
