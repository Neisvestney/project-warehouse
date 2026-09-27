using ProjectWarehouse.Server.Domain;

namespace ProjectWarehouse.Server.Models.Analytics;

/// <summary>The stored values as they are: null means "follow the system default", not "unset".</summary>
public class AnalyticsSettingsValuesDto
{
    public decimal? AbcBoundaryA { get; init; }
    public decimal? AbcBoundaryB { get; init; }
    public decimal? XyzBoundaryX { get; init; }
    public decimal? XyzBoundaryY { get; init; }
    public AnalyticsXyzStep? XyzStep { get; init; }
    public int? XyzMinIntervals { get; init; }
    public int? PayoutRatioWindowDays { get; init; }
    public int[]? PayoutAgeBoundaries { get; init; }
    public int? PayoutOverdueDays { get; init; }
    public int? ReturnsMaturityDays { get; init; }
}
