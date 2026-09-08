namespace ProjectWarehouse.Server.Models.Orders;

public class BatchFulfillResponse
{
    public IReadOnlyList<string> CompletedTaskIds { get; init; } = [];
    public IReadOnlyList<BatchFulfillFailedItem> FailedItems { get; init; } = [];

    /// <summary>
    /// The <c>insufficientInventory</c> failures of <see cref="FailedItems" /> folded per catalog item and
    /// storage node: one error carrying the summed shortfall instead of a line per position. Same shape as a
    /// single failure, so it renders through the usual error mechanism.
    /// </summary>
    public IReadOnlyList<AppFieldError> InsufficientInventoryErrors { get; init; } = [];
}

public class BatchFulfillFailedItem
{
    public Guid OrderId { get; init; }
    public Guid ComponentId { get; init; }
    /// <summary>Empty when the component itself could not be loaded.</summary>
    public string CatalogItemName { get; init; } = "";
    public required AppFieldError Error { get; init; }
}
