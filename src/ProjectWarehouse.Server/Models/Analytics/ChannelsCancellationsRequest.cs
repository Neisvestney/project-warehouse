namespace ProjectWarehouse.Server.Models.Analytics;

public class ChannelsCancellationsRequest : ChannelsFilterRequest
{
    /// <summary>Null picks one by the period length, as in the timeseries.</summary>
    public AnalyticsStep? Step { get; init; }
}
