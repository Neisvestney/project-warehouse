namespace ProjectWarehouse.Server.Domain;

/// <summary>
/// The one row of analytics calculation parameters, shared by the whole system so that "this item is in A"
/// means the same to everyone. Every field is nullable: null follows the constant in
/// <c>AnalyticsCalculator</c>, so changing a default reaches everyone who never chose a value.
/// </summary>
public class AnalyticsSettings
{
    public static readonly Guid SingletonId = new("5b0f7c1e-3a44-4f7e-9d7a-2c1e8a6b9f01");

    public Guid Id { get; set; }

    /// <summary>Percent, one decimal place.</summary>
    public decimal? AbcBoundaryA { get; set; }

    /// <summary>Percent, one decimal place.</summary>
    public decimal? AbcBoundaryB { get; set; }

    /// <summary>Coefficient of variation in percent, one decimal place.</summary>
    public decimal? XyzBoundaryX { get; set; }

    /// <summary>Coefficient of variation in percent, one decimal place.</summary>
    public decimal? XyzBoundaryY { get; set; }

    public AnalyticsXyzStep? XyzStep { get; set; }
    public int? XyzMinIntervals { get; set; }

    public int? PayoutRatioWindowDays { get; set; }

    /// <summary>Strictly ascending day boundaries of the in-transit age buckets.</summary>
    public int[]? PayoutAgeBoundaries { get; set; }

    public int? PayoutOverdueDays { get; set; }
    public int? PayoutNotAccruedDays { get; set; }
    public int? ReturnsMaturityDays { get; set; }

    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedById { get; set; }
    public ApplicationUser? UpdatedBy { get; set; }
}
