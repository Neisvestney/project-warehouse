namespace ProjectWarehouse.Server.Infrastructure;

public class UnitInventoryItemNotFoundException(Guid itemId)
    : Exception($"UnitInventoryItem '{itemId}' was not found.")
{
    public Guid ItemId { get; } = itemId;
}
