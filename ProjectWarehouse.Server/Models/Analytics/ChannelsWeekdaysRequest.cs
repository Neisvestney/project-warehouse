namespace ProjectWarehouse.Server.Models.Analytics;

public class ChannelsWeekdaysRequest : AnalyticsFilterRequest
{
    public AnalyticsMeasure Measure { get; init; } = AnalyticsMeasure.Orders;
}
