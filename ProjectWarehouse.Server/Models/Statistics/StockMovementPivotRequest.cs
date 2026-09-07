using System.ComponentModel.DataAnnotations;

namespace ProjectWarehouse.Server.Models.Statistics;

/// <summary>
/// The pivot is a POST because <see cref="Metrics"/> is a list of objects — it does not survive a query
/// string. Everything else is the shared movement filter.
/// </summary>
public class StockMovementPivotRequest : StockMovementFilterRequest
{
    [Range(1, 200)]
    public int ColumnLimit { get; init; } = 20;

    /// <summary>Sub-columns to compute, in display order. Empty means totals only.</summary>
    [MaxLength(StockMovementReportPresetLimits.MaxMetrics)]
    public IReadOnlyList<StockMovementMetricDto> Metrics { get; init; } = [];
}
