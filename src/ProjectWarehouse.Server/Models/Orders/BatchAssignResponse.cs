namespace ProjectWarehouse.Server.Models.Orders;

public class BatchAssignResponse
{
    public IReadOnlyList<Guid> AssignedOrderIds { get; init; } = [];
    public IReadOnlyList<BatchAssignFailedItem> FailedItems { get; init; } = [];
}

public class BatchAssignFailedItem
{
    public Guid OrderId { get; init; }
    /// <summary>Null when the order itself could not be loaded.</summary>
    public int? OrderNumber { get; init; }
    public required AppFieldError Error { get; init; }
}
