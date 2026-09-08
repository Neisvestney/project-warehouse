namespace ProjectWarehouse.Server.Models.Orders;

public class BatchFulfillRequest
{
    public IReadOnlyList<BatchFulfillItemRequest> Items { get; init; } = [];

    /// <summary>Mass-assembly mode: advance touched tasks (Done only when every component is fully fulfilled). Off for plain multi-fulfillment adds.</summary>
    public bool AutoCompleteTasks { get; init; }

    /// <summary>Keep the items that succeeded when others fail. Off means all-or-nothing: every item is still attempted and reported in <c>failedItems</c>, but a single failure rolls the whole batch back.</summary>
    public bool AllowPartialSuccess { get; init; }
}
