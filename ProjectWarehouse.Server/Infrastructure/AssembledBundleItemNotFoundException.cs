namespace ProjectWarehouse.Server.Infrastructure;

public class AssembledBundleItemNotFoundException(Guid itemId)
    : Exception($"AssembledBundleInventoryItem '{itemId}' was not found.")
{
    public Guid ItemId { get; } = itemId;
}
