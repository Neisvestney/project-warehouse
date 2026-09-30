namespace ProjectWarehouse.Server.Models.Analytics;

public class ChannelsReturnsRequest : ChannelsFilterRequest
{
    /// <summary>Returned money is listed in the Price mode only.</summary>
    public AnalyticsMoneyMode MoneyMode { get; init; } = AnalyticsMoneyMode.Price;

    /// <summary>Step of the volume-by-return-date series; null picks one by the period length.</summary>
    public AnalyticsStep? Step { get; init; }
}
