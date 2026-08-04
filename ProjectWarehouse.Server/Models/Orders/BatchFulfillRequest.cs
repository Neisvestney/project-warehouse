namespace ProjectWarehouse.Server.Models.Orders;

public class BatchFulfillRequest
{
    public IReadOnlyList<BatchFulfillItemRequest> Items { get; init; } = [];

    /// <summary>Mass-assembly mode: advance touched tasks (Done only when every component is fully fulfilled). Off for plain multi-fulfillment adds.</summary>
    public bool AutoCompleteTasks { get; init; }
}
