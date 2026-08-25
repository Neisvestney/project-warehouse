using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Infrastructure;
using ProjectWarehouse.Server.Infrastructure.Access;
using ProjectWarehouse.Server.Models.Forecast;
using ProjectWarehouse.Server.Services;

namespace ProjectWarehouse.Server.Controllers;

[Route("api/stock-forecast")]
[TimeZoneAware]
public class StockForecastController(IStockForecastService forecast, EntityAccessRegistry access)
    : AppControllerBase
{
    /// <summary>How many items may be asked for at once by the point-lookup endpoint.</summary>
    private const int MaxItemIds = 200;

    private EntityAccessRule<Warehouse> Rule => access.For<Warehouse>();

    /// <summary>How long the stock on one warehouse lasts, one page at a time.</summary>
    /// <remarks>
    /// Query params come from <c>StockForecastListRequest</c>: <c>warehouseId</c> (required),
    /// <c>searchString</c>, <c>catalogItemTypes</c>, <c>tagIds</c>, <c>isArchived</c>,
    /// <c>onlyWarnings</c>, <c>sortBy</c> (default <c>default</c>), <c>sortOrder</c>, plus <c>page</c>
    /// (default 1) and <c>pageSize</c> (default 20, max 200). Window, averaging mode and time zone are
    /// warehouse settings and are not accepted here; the applied values come back on the response so the
    /// client can label them.
    /// Only <c>Standard</c> and <c>Unit</c> items appear, and only those with stock or consumption in the
    /// window. <c>onlyWarnings</c> keeps <c>outOfStock</c> and <c>warning</c>.
    /// Requires <c>statistics.view</c> or <c>statistics.view_assigned</c>, and view access to the
    /// warehouse itself (<c>warehouses.view</c> / <c>warehouses.view_assigned</c>): another warehouse
    /// answers 403 <c>warehouseNotAssigned</c> rather than an empty page, and a caller with neither
    /// statistics permission gets 403 <c>permissionDenied</c>.
    /// Returns 422 <c>required</c> on <c>warehouseId</c> when it is missing and 422
    /// <c>warehouseNotFound</c> when it names nothing (no <c>args</c> on either).
    /// </remarks>
    [HttpGet]
    [Authorize]
    [ProducesResponseType<StockForecastListDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetList(
        [FromQuery] StockForecastListRequest request,
        [FromQuery] [Range(1, int.MaxValue)] int page = 1,
        [FromQuery] [Range(1, 200)] int pageSize = 20,
        CancellationToken ct = default)
    {
        if (await CheckReadAsync(request.WarehouseId!.Value, ct) is { } denied) return denied;

        try
        {
            return Ok(await forecast.GetListAsync(User, request, page, pageSize, ct));
        }
        catch (Infrastructure.ValidationException ex)
        {
            return UnprocessableEntity(ex);
        }
    }

    /// <summary>The same numbers for a named set of items, without paging or filters.</summary>
    /// <remarks>
    /// Query params: <c>warehouseId</c> (required) and <c>catalogItemIds</c> (at most 200; an empty list
    /// returns an empty map).
    /// The response is keyed by catalog item id; an item with neither stock nor consumption on the
    /// warehouse, or one of a virtual type, is simply absent from it.
    /// Same access rule and the same 422 <c>required</c> / <c>warehouseNotFound</c> codes as
    /// <c>GET /api/stock-forecast</c>, plus 422 <c>outOfRange</c> on <c>catalogItemIds</c> when more
    /// than 200 are passed (no <c>args</c>) — in practice a query string that long is refused by the
    /// host with a bare 414 first.
    /// </remarks>
    [HttpGet("items")]
    [Authorize]
    [ProducesResponseType<IReadOnlyDictionary<Guid, StockForecastDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetForItems(
        [FromQuery] [Required] Guid? warehouseId,
        [FromQuery] Guid[]? catalogItemIds,
        CancellationToken ct = default)
    {
        if (await CheckReadAsync(warehouseId!.Value, ct) is { } denied) return denied;

        var ids = catalogItemIds ?? [];
        if (ids.Length > MaxItemIds)
            return UnprocessableEntity("catalogItemIds", ErrorCode.OutOfRange,
                $"No more than {MaxItemIds} catalog items may be requested at once.");

        try
        {
            return Ok(await forecast.GetForItemsAsync(User, warehouseId.Value, ids, ct: ct));
        }
        catch (Infrastructure.ValidationException ex)
        {
            return UnprocessableEntity(ex);
        }
    }

    /// <summary>Forecast settings of one warehouse, next to the system defaults.</summary>
    /// <remarks>
    /// A null <c>stockWarningDays</c> or <c>consumptionWindowDays</c> means the warehouse follows the
    /// system default rather than having chosen the same number; <c>effective*</c> carries what the
    /// forecast will actually use, and <c>effectiveTimeZoneId</c> the zone after the whole fallback chain.
    /// Requires <c>warehouses.edit</c> or <c>warehouses.edit_assigned</c> — these are report parameters
    /// only the person who may edit the warehouse changes. Another warehouse answers 403
    /// <c>warehouseNotAssigned</c>. Returns 422 <c>warehouseNotFound</c> on <c>warehouseId</c> when the
    /// warehouse does not exist (no <c>args</c>).
    /// </remarks>
    [HttpGet("settings/{warehouseId:guid}")]
    [Authorize]
    [ProducesResponseType<StockForecastSettingsDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSettings(Guid warehouseId, CancellationToken ct = default)
    {
        if (AccessError(await Rule.CheckWarehouseAsync(User, AccessLevel.Edit, warehouseId, ct)) is { } denied)
            return denied;

        try
        {
            return Ok(await forecast.GetSettingsAsync(User, warehouseId, ct));
        }
        catch (Infrastructure.ValidationException ex)
        {
            return UnprocessableEntity(ex);
        }
    }

    /// <summary>Writes the forecast settings of one warehouse.</summary>
    /// <remarks>
    /// Body: <c>stockWarningDays</c> (0..3650, null restores the default), <c>consumptionWindowDays</c>
    /// (1..366, null restores the default), <c>useWeightedConsumption</c>, <c>timeZoneId</c> (IANA, null
    /// falls back to the caller's zone and then to the server's). The window cap matches the statistics
    /// endpoints and keeps a request from scanning the whole journal.
    /// Requires <c>warehouses.edit</c> or <c>warehouses.edit_assigned</c>; another warehouse answers 403
    /// <c>warehouseNotAssigned</c>.
    /// Returns 422 <c>validationError</c> on a day field outside its range, 422 <c>invalidValue</c> on
    /// <c>timeZoneId</c> when the identifier is unknown, and 422 <c>warehouseNotFound</c> on
    /// <c>warehouseId</c> (no <c>args</c> on any of them).
    /// </remarks>
    [HttpPut("settings/{warehouseId:guid}")]
    [Authorize]
    [ProducesResponseType<StockForecastSettingsDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateSettings(
        Guid warehouseId,
        [FromBody] UpdateStockForecastSettingsRequest request,
        CancellationToken ct = default)
    {
        if (AccessError(await Rule.CheckWarehouseAsync(User, AccessLevel.Edit, warehouseId, ct)) is { } denied)
            return denied;

        try
        {
            return Ok(await forecast.UpdateSettingsAsync(User, warehouseId, request, ct));
        }
        catch (Infrastructure.ValidationException ex)
        {
            return UnprocessableEntity(ex);
        }
    }

    /// <summary>Sets or clears the warning threshold of one item on one warehouse.</summary>
    /// <remarks>
    /// Body: <c>warehouseId</c>, <c>catalogItemId</c>, <c>warningDays</c> (0..3650). A null
    /// <c>warningDays</c> deletes the override, so the item goes back to inheriting the warehouse
    /// setting instead of freezing at its current value.
    /// Requires <c>warehouses.edit</c> or <c>warehouses.edit_assigned</c>; another warehouse answers 403
    /// <c>warehouseNotAssigned</c>.
    /// Returns 422 <c>required</c> on a missing body field, 422 <c>validationError</c> on
    /// <c>warningDays</c> outside 0..3650, 422 <c>warehouseNotFound</c> on <c>warehouseId</c>,
    /// 422 <c>catalogItemNotFound</c> and 422 <c>invalidValue</c> on <c>catalogItemId</c> — the latter
    /// when the item is of a virtual type and holds no stock (no <c>args</c> on any of them).
    /// </remarks>
    [HttpPut("overrides")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> SetOverride(
        [FromBody] SetStockWarningOverrideRequest request,
        CancellationToken ct = default)
    {
        if (AccessError(await Rule.CheckWarehouseAsync(User, AccessLevel.Edit, request.WarehouseId, ct))
            is { } denied)
            return denied;

        try
        {
            await forecast.SetOverrideAsync(User, request, ct);
            return NoContent();
        }
        catch (Infrastructure.ValidationException ex)
        {
            return UnprocessableEntity(ex);
        }
    }

    /// <summary>
    /// The forecast is a derivative of the movement journal, so it takes the journal's permission; the
    /// warehouse itself is then judged by its own rule, which is what turns someone else's warehouse
    /// into a 403 instead of an empty page.
    /// </summary>
    private async Task<IActionResult?> CheckReadAsync(Guid warehouseId, CancellationToken ct)
    {
        if (!User.HasClaim("permission", Permissions.Statistics.View)
            && !User.HasClaim("permission", Permissions.Statistics.ViewAssigned))
            return Forbidden();

        return AccessError(await Rule.CheckWarehouseAsync(User, AccessLevel.View, warehouseId, ct));
    }
}
