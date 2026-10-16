using ProjectWarehouse.Server.Models.Statistics;

namespace ProjectWarehouse.Server.Services;

/// <summary>
/// Column layouts of the movement report. They are global — no warehouse scoping, no per-user copies —
/// so every method here works on the single shared list.
/// </summary>
public interface IStockMovementPresetService
{
    /// <summary>Default first, then by name.</summary>
    Task<IReadOnlyList<StockMovementReportPresetDto>> GetAllAsync(CancellationToken ct = default);

    Task<StockMovementReportPresetDto> CreateAsync(
        SaveStockMovementReportPresetRequest request,
        Guid? actorId,
        CancellationToken ct = default);

    /// <summary>Returns null when the preset is gone.</summary>
    Task<(StockMovementReportPresetDto Before, StockMovementReportPresetDto After)?> UpdateAsync(
        Guid id,
        SaveStockMovementReportPresetRequest request,
        Guid? actorId,
        CancellationToken ct = default);

    /// <summary>Returns the deleted preset, or null when it was already gone.</summary>
    Task<StockMovementReportPresetDto?> DeleteAsync(Guid id, CancellationToken ct = default);
}
