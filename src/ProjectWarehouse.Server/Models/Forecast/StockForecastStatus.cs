namespace ProjectWarehouse.Server.Models.Forecast;

/// <summary>
/// How urgent the position is. <see cref="OutOfStock"/> does not depend on the threshold at all:
/// stock ran out while it was still being consumed, which is a warning at any <c>warningDays</c>,
/// zero included. <see cref="NoConsumption"/> is not a warning — dead stock needs a different
/// conversation, not a purchase order.
/// </summary>
public enum StockForecastStatus
{
    OutOfStock = 1,
    Warning = 2,
    Ok = 3,
    NoConsumption = 4,
}
