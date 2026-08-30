using ProjectWarehouse.Server.Domain;

namespace ProjectWarehouse.Server.Models.Orders;

public class BatchTransitionStatusRequest
{
    public IReadOnlyList<Guid> OrderIds { get; init; } = [];
    public OrderStatus TargetStatus { get; init; }
}
