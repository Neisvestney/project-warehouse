namespace ProjectWarehouse.Server.Models.Orders;

public class BatchSelfAssignRequest
{
    public IReadOnlyList<Guid> OrderIds { get; init; } = [];
}
