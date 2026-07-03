namespace ProjectWarehouse.Server.Models.Orders;

public class BatchFulfillRequest
{
    public IReadOnlyList<BatchFulfillItemRequest> Items { get; init; } = [];
}
