using ProjectWarehouse.Server.Infrastructure;

namespace ProjectWarehouse.Server.Models.InboundOrderProcessing;

public class ProcessingStoragePlaceDto : IHasIdentity
{
    public Guid Id { get; init; }
    public string Name { get; init; } = null!;
    public decimal X { get; init; }
    public decimal Y { get; init; }
    public decimal Width { get; init; }
    public decimal Height { get; init; }
    public decimal Rotation { get; init; }
    public bool HasOrderItems { get; init; }
}
