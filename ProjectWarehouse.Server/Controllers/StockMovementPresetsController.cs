using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectWarehouse.Server.Infrastructure;
using ProjectWarehouse.Server.Infrastructure.ChangeLog;
using ProjectWarehouse.Server.Models.Statistics;
using ProjectWarehouse.Server.Services;

namespace ProjectWarehouse.Server.Controllers;

/// <summary>
/// Column layouts of the movement report. One shared list — there are no per-user presets, so editing
/// one is visible to everybody who can open the report, and the right to edit is the right to view.
/// </summary>
[Route("api/statistics/movement-presets")]
public class StockMovementPresetsController(
    IStockMovementPresetService presets,
    IChangeLogService<StockMovementReportPresetDto> changeLog) : AppControllerBase
{
    /// <summary>All presets, the default one first.</summary>
    /// <remarks>
    /// Not paginated — the list is small by design. Requires <c>statistics.view</c> or
    /// <c>statistics.view_assigned</c>; 403 <c>permissionDenied</c> when neither is held. Presets are not
    /// warehouse-scoped, so <c>view_assigned</c> sees the same list as <c>view</c>.
    /// </remarks>
    [HttpGet]
    [Authorize]
    [ProducesResponseType<List<StockMovementReportPresetDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPresets(CancellationToken ct = default)
    {
        if (CheckAccess() is { } forbidden) return forbidden;

        return Ok(await presets.GetAllAsync(ct));
    }

    /// <summary>Create a preset.</summary>
    /// <remarks>
    /// Errors:
    /// <list type="bullet">
    ///   <item>422 <c>stockMovementPresetNameDuplicate</c> on <c>name</c></item>
    ///   <item>422 <c>required</c> on <c>metrics[i].name</c></item>
    ///   <item>422 <c>stockMovementPresetUnknownAction</c> on <c>metrics[i].actions</c></item>
    /// </list>
    /// The first preset created becomes the default whatever <c>isDefault</c> says — the report has no
    /// columns without one. Same access rule as the listing.
    /// </remarks>
    [HttpPost]
    [Authorize]
    [ProducesResponseType<StockMovementReportPresetDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> CreatePreset(
        [FromBody] SaveStockMovementReportPresetRequest request,
        CancellationToken ct = default)
    {
        if (CheckAccess() is { } forbidden) return forbidden;

        try
        {
            var dto = await presets.CreateAsync(request, GetCurrentUserId(), ct);
            await changeLog.CompareAndSaveToChangelog(null, dto);
            return Ok(dto);
        }
        catch (Infrastructure.ValidationException ex)
        {
            return UnprocessableEntity(ex);
        }
    }

    /// <summary>Update a preset.</summary>
    /// <remarks>
    /// 404 <c>stockMovementPresetNotFound</c> when the preset is gone, 422 <c>required</c> on
    /// <c>version</c> when it is missing and 409 <c>stockMovementPresetModified</c> when it is stale —
    /// presets are shared, so the <c>version</c> the edit started from is mandatory here. Plus the same
    /// 422 codes as creation. Same access rule as the listing.
    /// </remarks>
    [HttpPut("{id:guid}")]
    [Authorize]
    [ProducesResponseType<StockMovementReportPresetDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdatePreset(
        Guid id,
        [FromBody] SaveStockMovementReportPresetRequest request,
        CancellationToken ct = default)
    {
        if (CheckAccess() is { } forbidden) return forbidden;

        try
        {
            var result = await presets.UpdateAsync(id, request, GetCurrentUserId(), ct);
            if (result is null)
                return NotFound(ErrorCode.StockMovementPresetNotFound, "Preset not found.");

            await changeLog.CompareAndSaveToChangelog(result.Value.Before, result.Value.After);
            return Ok(result.Value.After);
        }
        catch (Infrastructure.ValidationException ex)
        {
            return UnprocessableEntity(ex);
        }
        catch (StockMovementPresetConflictException)
        {
            return Conflict(ErrorCode.StockMovementPresetModified,
                "The preset was changed by someone else. Reload it and apply the change again.");
        }
    }

    /// <summary>Delete a preset. The default flag moves to the next preset by name.</summary>
    /// <remarks>
    /// 404 <c>stockMovementPresetNotFound</c> when the preset is gone, 422
    /// <c>stockMovementPresetLastOne</c> on <c>root</c> when it is the only one left, 409
    /// <c>stockMovementPresetModified</c> when someone edited it under the delete. Same access rule as
    /// the listing.
    /// </remarks>
    [HttpDelete("{id:guid}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeletePreset(Guid id, CancellationToken ct = default)
    {
        if (CheckAccess() is { } forbidden) return forbidden;

        try
        {
            var before = await presets.DeleteAsync(id, ct);
            if (before is null)
                return NotFound(ErrorCode.StockMovementPresetNotFound, "Preset not found.");

            await changeLog.CompareAndSaveToChangelog(before, null);
            return NoContent();
        }
        catch (Infrastructure.ValidationException ex)
        {
            return UnprocessableEntity(ex);
        }
        catch (StockMovementPresetConflictException)
        {
            return Conflict(ErrorCode.StockMovementPresetModified,
                "The preset was changed by someone else. Reload the list and try again.");
        }
    }

    /// <summary>
    /// The same pair as <see cref="StatisticsController"/>: a preset is display state of that report, and
    /// there is nothing in it to scope by warehouse.
    /// </summary>
    private ObjectResult? CheckAccess() =>
        User.HasClaim("permission", Permissions.Statistics.View) ||
        User.HasClaim("permission", Permissions.Statistics.ViewAssigned)
            ? null
            : Forbidden();
}
