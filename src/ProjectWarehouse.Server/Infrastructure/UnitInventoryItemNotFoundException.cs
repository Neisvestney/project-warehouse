namespace ProjectWarehouse.Server.Infrastructure;

public class UnitInventoryItemNotFoundException(Guid itemId)
    : Exception($"UnitInventoryItem '{itemId}' was not found."), IExpectedFailure
{
    public Guid ItemId { get; } = itemId;
}
