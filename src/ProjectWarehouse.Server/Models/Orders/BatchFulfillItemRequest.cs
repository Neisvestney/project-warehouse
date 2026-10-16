namespace ProjectWarehouse.Server.Models.Orders;

public class BatchFulfillItemRequest
{
    public Guid OrderId { get; init; }
    public Guid TaskId { get; init; }
    public Guid TaskBoxId { get; init; }
    public Guid ComponentId { get; init; }
    public AddFulfillmentRequest Fulfillment { get; init; } = null!;
}
