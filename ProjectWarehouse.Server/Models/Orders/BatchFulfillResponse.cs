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
    /// <summary>Empty when the component itself could not be loaded.</summary>
    public string CatalogItemName { get; init; } = "";
    public required AppFieldError Error { get; init; }
}
