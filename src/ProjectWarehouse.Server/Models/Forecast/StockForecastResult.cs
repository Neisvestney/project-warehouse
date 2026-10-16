namespace ProjectWarehouse.Server.Models.Forecast;

/// <summary>What <see cref="Infrastructure.StockForecastCalculator"/> produces for one position.</summary>
public readonly record struct StockForecastResult(
    decimal DailyConsumption,
    int ConsumedInWindow,
    int? DaysLeft,
    StockForecastStatus Status);
