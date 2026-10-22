namespace ProjectWarehouse.Server.Models.Orders;

public class OrderCompositionSummaryRequest
{
    public IReadOnlyList<Guid> OrderIds { get; init; } = [];
}
