namespace ProjectWarehouse.Server.Models.Analytics;

public class ChannelsSummaryRequest : ChannelsFilterRequest
{
    public AnalyticsMoneyMode MoneyMode { get; init; } = AnalyticsMoneyMode.Price;
}
