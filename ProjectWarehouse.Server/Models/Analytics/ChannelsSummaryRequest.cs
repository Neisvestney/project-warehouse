namespace ProjectWarehouse.Server.Models.Analytics;

public class ChannelsSummaryRequest : AnalyticsFilterRequest
{
    public AnalyticsMoneyMode MoneyMode { get; init; } = AnalyticsMoneyMode.Price;
}
