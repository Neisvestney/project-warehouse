namespace ProjectWarehouse.Server.Services;

/// <summary>
/// Carries a marketplace warehouse's mapping onto the external orders imported while it had none. Only a blank
/// is filled: an external order that already names a WMS warehouse keeps it when the mapping changes.
/// </summary>
public interface IExternalOrderWarehouseService
{
    /// <summary>
    /// Gives the account's external FBS orders that ship from <paramref name="warehouseExternalId"/> and have
    /// no WMS warehouse the one at <paramref name="warehouseId"/>. Returns the ids of the orders bound.
    /// </summary>
    Task<IReadOnlyList<Guid>> BindUnmappedAsync(Guid accountId, string warehouseExternalId, Guid warehouseId,
        CancellationToken ct, TimeSpan? lockTimeout = null);
}
