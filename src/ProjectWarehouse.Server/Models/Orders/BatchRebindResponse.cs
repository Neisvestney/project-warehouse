namespace ProjectWarehouse.Server.Models.Orders;

public class BatchRebindResponse
{
    public IReadOnlyList<Guid> ReboundOrderIds { get; init; } = [];
    public IReadOnlyList<Guid> UnchangedOrderIds { get; init; } = [];
    public IReadOnlyList<BatchRebindFailedItem> FailedItems { get; init; } = [];
}

public class BatchRebindFailedItem
{
    public Guid OrderId { get; init; }
    /// <summary>Null when the order itself could not be loaded.</summary>
    public int? OrderNumber { get; init; }
    public required AppFieldError Error { get; init; }
}
