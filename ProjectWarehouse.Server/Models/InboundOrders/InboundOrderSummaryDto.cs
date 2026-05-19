using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Infrastructure;
using ProjectWarehouse.Server.Models.Warehouses;

namespace ProjectWarehouse.Server.Models.InboundOrders;

public class InboundOrderSummaryDto : IHasIdentity
{
    public Guid Id { get; init; }
    public int Number { get; init; }
    public InboundOrderStatus Status { get; init; }
    public string? Title { get; init; }
    public DateTime PlannedStartDateTime { get; init; }
    public WarehouseSummaryDto Warehouse { get; init; } = null!;
}
