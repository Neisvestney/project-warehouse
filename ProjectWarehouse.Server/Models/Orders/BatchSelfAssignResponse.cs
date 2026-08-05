namespace ProjectWarehouse.Server.Models.Orders;

public class BatchSelfAssignResponse
{
    public IReadOnlyList<Guid> AssignedOrderIds { get; init; } = [];
    public IReadOnlyList<BatchSelfAssignFailedItem> FailedItems { get; init; } = [];
}

public class BatchSelfAssignFailedItem
{
    public Guid OrderId { get; init; }
    /// <summary>Null when the order itself could not be loaded.</summary>
    public int? OrderNumber { get; init; }
    public required AppFieldError Error { get; init; }
}
