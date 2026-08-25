namespace ProjectWarehouse.Server.Models.Forecast;

/// <summary>
/// <see cref="Default"/> is the composite rule — warnings first, then by days left, "never" last.
/// Any other value replaces that rule entirely; a null <c>daysLeft</c> still sorts last either way.
/// </summary>
public enum StockForecastSortBy
{
    Default = 0,
    Type = 1,
    Name = 2,
    Article = 3,
    Stock = 4,
    DailyConsumption = 5,
    DaysLeft = 6,
}
