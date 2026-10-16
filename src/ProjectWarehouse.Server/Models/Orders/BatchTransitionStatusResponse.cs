namespace ProjectWarehouse.Server.Models.Orders;

public class BatchTransitionStatusResponse
{
    public IReadOnlyList<Guid> TransitionedOrderIds { get; init; } = [];
    public IReadOnlyList<BatchTransitionStatusFailedItem> FailedItems { get; init; } = [];
}

public class BatchTransitionStatusFailedItem
{
    public Guid OrderId { get; init; }
    /// <summary>Null when the order itself could not be loaded.</summary>
    public int? OrderNumber { get; init; }
    public required AppFieldError Error { get; init; }
}
