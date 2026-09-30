namespace ProjectWarehouse.Server.Models.Analytics;

public class ChannelsTimeseriesRequest : ChannelsFilterRequest
{
    /// <summary>Null picks one by the period length: day up to 31 days, week up to six months, month beyond.</summary>
    public AnalyticsStep? Step { get; init; }

    public AnalyticsMeasure Measure { get; init; } = AnalyticsMeasure.Orders;
}
