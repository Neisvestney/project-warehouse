using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Infrastructure;

namespace ProjectWarehouse.Server.Models.InboundOrderProcessing;

public class InboundOrderProcessingDto : IHasIdentity
{
    public Guid Id { get; init; }
    public int Number { get; init; }
    public InboundOrderStatus Status { get; init; }
    public string? Title { get; init; }
    public DateTime PlannedStartDateTime { get; init; }
    public string? Notes { get; init; }
    public ProcessingWarehouseDto Warehouse { get; init; } = null!;
}
