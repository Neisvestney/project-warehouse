namespace ProjectWarehouse.Server.Infrastructure;

/// <summary>
/// Raised when a stock change lost the optimistic-concurrency race too many times in a row. Nothing was
/// written, so the operation is safe to repeat — unlike a genuine <c>DbUpdateException</c>, which is
/// why contention gets its own code instead of surfacing as a server error.
/// </summary>
public class InventoryWriteConflictException(Guid nodeId, Guid catalogItemId, int attempts, Exception inner)
    : Exception(
        $"Stock for catalog item '{catalogItemId}' in node '{nodeId}' kept changing under {attempts} attempts.",
        inner)
{
    public Guid NodeId { get; } = nodeId;
    public Guid CatalogItemId { get; } = catalogItemId;
    public int Attempts { get; } = attempts;
}
