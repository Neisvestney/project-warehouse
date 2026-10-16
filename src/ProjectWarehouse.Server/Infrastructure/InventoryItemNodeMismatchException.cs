namespace ProjectWarehouse.Server.Infrastructure;

/// <summary>
/// Thrown when a Remove operation finds the item in a different node than expected.
/// This indicates the item was moved after the placement was recorded.
/// </summary>
public class InventoryItemNodeMismatchException(Guid itemId, Guid expectedNodeId, Guid actualNodeId)
    : Exception($"Item '{itemId}' is not in the expected node '{expectedNodeId}' (actual: '{actualNodeId}'). It may have been moved after the placement was created."), IExpectedFailure
{
    public Guid ItemId { get; } = itemId;
    public Guid ExpectedNodeId { get; } = expectedNodeId;
    public Guid ActualNodeId { get; } = actualNodeId;
}
