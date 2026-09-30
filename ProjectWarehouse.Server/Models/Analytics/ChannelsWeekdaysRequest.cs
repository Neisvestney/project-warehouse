namespace ProjectWarehouse.Server.Models.Analytics;

public class ChannelsWeekdaysRequest : ChannelsFilterRequest
{
    public AnalyticsMeasure Measure { get; init; } = AnalyticsMeasure.Orders;
    public WeekdayScale Scale { get; init; } = WeekdayScale.Average;
}
