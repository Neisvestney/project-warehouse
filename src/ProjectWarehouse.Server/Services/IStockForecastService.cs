using System.Security.Claims;
using ProjectWarehouse.Server.Models.Forecast;

namespace ProjectWarehouse.Server.Services;

/// <summary>
/// How long the stock on a warehouse lasts at the current rate of consumption. Nothing here is stored:
/// the forecast is a pure derivative of the on-hand quantity and the movement journal, and any
/// denormalization of it would go stale on the first shipment.
/// </summary>
public interface IStockForecastService
{
    /// <summary>A page of the forecast for one warehouse, plus the settings it was computed under.</summary>
    Task<StockForecastListDto> GetListAsync(
        ClaimsPrincipal user,
        StockForecastListRequest request,
        int page,
        int pageSize,
        CancellationToken ct = default);

    /// <summary>
    /// The same arithmetic for a named set of items, without paging or filters — the entry point for
    /// showing a forecast where the item is already on screen.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, StockForecastDto>> GetForItemsAsync(
        ClaimsPrincipal user,
        Guid warehouseId,
        IReadOnlyCollection<Guid> catalogItemIds,
        StockForecastOptions? options = null,
        CancellationToken ct = default);

    /// <summary>
    /// System entry: the warehouse is named explicitly and no permission is checked at all. A background
    /// job has no caller, and handing it a synthetic principal with full rights would put an account in
    /// the system that nobody granted and that walks past every access rule silently. Who may see the
    /// result is the caller's responsibility.
    /// </summary>
    Task<IReadOnlyList<StockForecastRowDto>> ComputeForWarehouseAsync(
        Guid warehouseId,
        StockForecastScope scope,
        StockForecastOptions? options = null,
        CancellationToken ct = default);

    Task<StockForecastSettingsDto> GetSettingsAsync(
        ClaimsPrincipal user, Guid warehouseId, CancellationToken ct = default);

    Task<StockForecastSettingsDto> UpdateSettingsAsync(
        ClaimsPrincipal user, Guid warehouseId, UpdateStockForecastSettingsRequest request,
        CancellationToken ct = default);

    Task SetOverrideAsync(
        ClaimsPrincipal user, SetStockWarningOverrideRequest request, CancellationToken ct = default);
}
