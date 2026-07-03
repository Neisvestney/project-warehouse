namespace ProjectWarehouse.Server.Models.Orders;

public class BatchFulfillResponse
{
    public IReadOnlyList<string> CompletedTaskIds { get; init; } = [];
    public IReadOnlyList<BatchFulfillFailedItem> FailedItems { get; init; } = [];
}

public class BatchFulfillFailedItem
{
    public Guid OrderId { get; init; }
    public Guid ComponentId { get; init; }
    public string Error { get; init; } = "";
}
