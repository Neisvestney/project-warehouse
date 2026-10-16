namespace ProjectWarehouse.Server.Services;

/// <summary>
/// The one place that answers "whose day is this". Warehouse zone → caller's <c>X-Time-Zone</c> →
/// server zone, in that order: a Moscow warehouse is cut by Moscow no matter who is looking, so the
/// page, the report and the alert all quote the same number.
/// </summary>
public interface IWarehouseTimeZoneResolver
{
    /// <summary>
    /// Never throws and never returns null: an unknown or malformed identifier at any level falls
    /// through to the next one, and the server zone is always available.
    /// </summary>
    Task<TimeZoneInfo> ResolveAsync(Guid? warehouseId, CancellationToken ct = default);
}
