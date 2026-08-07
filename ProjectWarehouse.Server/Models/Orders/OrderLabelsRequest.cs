namespace ProjectWarehouse.Server.Models.Orders;

public class OrderLabelsRequest
{
    public IReadOnlyList<Guid> OrderIds { get; init; } = [];
}
