using System.ComponentModel.DataAnnotations;
using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Infrastructure;

namespace ProjectWarehouse.Server.Models.Analytics;

/// <summary>A full write of every field; null restores the system default.</summary>
public class UpdateAnalyticsSettingsRequest
{
    [Range(AnalyticsCalculator.MinAbcBoundaryA, AnalyticsCalculator.MaxAbcBoundaryA)]
    public decimal? AbcBoundaryA { get; init; }

    [Range(AnalyticsCalculator.MinAbcBoundaryB, AnalyticsCalculator.MaxAbcBoundaryB)]
    public decimal? AbcBoundaryB { get; init; }

    [Range(AnalyticsCalculator.MinXyzBoundaryX, AnalyticsCalculator.MaxXyzBoundaryX)]
    public decimal? XyzBoundaryX { get; init; }

    [Range(AnalyticsCalculator.MinXyzBoundaryY, AnalyticsCalculator.MaxXyzBoundaryY)]
    public decimal? XyzBoundaryY { get; init; }

    public AnalyticsXyzStep? XyzStep { get; init; }

    [Range(AnalyticsCalculator.MinXyzMinIntervals, AnalyticsCalculator.MaxXyzMinIntervals)]
    public int? XyzMinIntervals { get; init; }

    [Range(AnalyticsCalculator.MinPayoutRatioWindowDays, AnalyticsCalculator.MaxPayoutRatioWindowDays)]
    public int? PayoutRatioWindowDays { get; init; }

    /// <summary>1..5 values, strictly ascending, each within 1..365.</summary>
    public int[]? PayoutAgeBoundaries { get; init; }

    [Range(AnalyticsCalculator.MinPayoutOverdueDays, AnalyticsCalculator.MaxPayoutOverdueDays)]
    public int? PayoutOverdueDays { get; init; }

    [Range(AnalyticsCalculator.MinPayoutNotAccruedDays, AnalyticsCalculator.MaxPayoutNotAccruedDays)]
    public int? PayoutNotAccruedDays { get; init; }

    [Range(AnalyticsCalculator.MinReturnsMaturityDays, AnalyticsCalculator.MaxReturnsMaturityDays)]
    public int? ReturnsMaturityDays { get; init; }

    /// <summary>The version the edit started from; a save by someone else since then is a 409.</summary>
    [Required]
    public uint? Version { get; init; }
}
