using System.ComponentModel.DataAnnotations;
using ProjectWarehouse.Server.Infrastructure;

namespace ProjectWarehouse.Server.Models.Forecast;

public class UpdateStockForecastSettingsRequest
{
    /// <summary>Null restores the system default.</summary>
    [Range(0, StockForecastCalculator.MaxWarningDays)]
    public int? StockWarningDays { get; init; }

    /// <summary>Null restores the system default.</summary>
    [Range(StockForecastCalculator.MinWindowDays, StockForecastCalculator.MaxWindowDays)]
    public int? ConsumptionWindowDays { get; init; }

    public bool UseWeightedConsumption { get; init; }

    /// <summary>IANA identifier; null falls back to the caller's zone and then to the server's.</summary>
    public string? TimeZoneId { get; init; }
}
