namespace ProjectWarehouse.Server.Models.Forecast;

/// <summary>
/// The warehouse's own settings next to the system defaults. Null and "equal to the default" are
/// different states and stay distinguishable: a null keeps following the constant when it changes.
/// </summary>
public class StockForecastSettingsDto
{
    public Guid WarehouseId { get; init; }

    public int? StockWarningDays { get; init; }
    public int? ConsumptionWindowDays { get; init; }
    public bool UseWeightedConsumption { get; init; }
    public string? TimeZoneId { get; init; }

    public int DefaultWarningDays { get; init; }
    public int DefaultWindowDays { get; init; }

    public int EffectiveWarningDays { get; init; }
    public int EffectiveWindowDays { get; init; }

    /// <summary>Zone the days would actually be cut in, once the fallback chain has run.</summary>
    public string EffectiveTimeZoneId { get; init; } = null!;
}
