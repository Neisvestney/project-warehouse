using ProjectWarehouse.Server.Domain;

namespace ProjectWarehouse.Server.Models.Analytics;

/// <summary>
/// Calculation parameters after a stored null has been replaced by the system default. Every report
/// works only with this, and the responses that depend on it echo the values so the UI labels classes,
/// windows and age columns by them rather than by constants of its own.
/// </summary>
public class AnalyticsOptions
{
    public required decimal AbcBoundaryA { get; init; }
    public required decimal AbcBoundaryB { get; init; }
    public required decimal XyzBoundaryX { get; init; }
    public required decimal XyzBoundaryY { get; init; }
    public required AnalyticsXyzStep XyzStep { get; init; }
    public required int XyzMinIntervals { get; init; }
    public required int PayoutRatioWindowDays { get; init; }
    public required IReadOnlyList<int> PayoutAgeBoundaries { get; init; }
    public required int PayoutOverdueDays { get; init; }
    public required int ReturnsMaturityDays { get; init; }
}
