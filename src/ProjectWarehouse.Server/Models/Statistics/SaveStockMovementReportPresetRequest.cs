using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ProjectWarehouse.Server.Models.Statistics;

public class SaveStockMovementReportPresetRequest
{
    [JsonRequired]
    [Required, MaxLength(100)]
    public string Name { get; init; } = null!;

    /// <summary>Setting it clears the flag on whichever preset carried it before.</summary>
    public bool IsDefault { get; init; }

    [JsonRequired]
    [MinLength(1), MaxLength(StockMovementReportPresetLimits.MaxMetrics)]
    public IReadOnlyList<StockMovementMetricDto> Metrics { get; init; } = [];

    /// <summary>
    /// The <c>version</c> the edit started from. Required on update — presets are shared, and without it
    /// the second of two concurrent saves would silently win.
    /// </summary>
    public uint? Version { get; init; }
}

public static class StockMovementReportPresetLimits
{
    /// <summary>Every metric is a column per item, so the cap here is what keeps the table scrollable.</summary>
    public const int MaxMetrics = 12;
}
