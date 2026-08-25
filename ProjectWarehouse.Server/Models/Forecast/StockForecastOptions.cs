namespace ProjectWarehouse.Server.Models.Forecast;

/// <summary>
/// Everything the numbers depend on besides the data itself. Resolved from the warehouse settings on
/// every endpoint; passed explicitly only by backend callers computing against a window that is not
/// the warehouse's own.
/// </summary>
public class StockForecastOptions
{
    public required int WindowDays { get; init; }

    public required bool UseWeightedConsumption { get; init; }

    /// <summary>IANA zone the days of the window are cut in.</summary>
    public required string TimeZoneId { get; init; }

    /// <summary>The zone collapsed to a fixed offset for this calculation.</summary>
    public required int OffsetMinutes { get; init; }
}
