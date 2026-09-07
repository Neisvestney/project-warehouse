namespace ProjectWarehouse.Server.Infrastructure;

/// <summary>
/// Raised when a preset was edited by someone else between the read and the save. Presets are shared, so
/// this is ordinary traffic rather than a fault — the caller reloads and reapplies.
/// </summary>
public class StockMovementPresetConflictException(Guid presetId, Exception? inner = null)
    : Exception($"Stock movement report preset '{presetId}' was modified by another user.", inner), IExpectedFailure
{
    public Guid PresetId { get; } = presetId;

    ErrorCode? IExpectedFailure.Code => ErrorCode.StockMovementPresetModified;
}
