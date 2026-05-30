namespace ProjectWarehouse.Server.Infrastructure;

public class InsufficientInventoryException(Guid nodeId, Guid catalogItemId, int available, int requested)
    : Exception($"Insufficient inventory for catalog item '{catalogItemId}' in node '{nodeId}': requested {requested}, available {available}.")
{
    public Guid NodeId { get; } = nodeId;
    public Guid CatalogItemId { get; } = catalogItemId;
    public int Available { get; } = available;
    public int Requested { get; } = requested;
}
