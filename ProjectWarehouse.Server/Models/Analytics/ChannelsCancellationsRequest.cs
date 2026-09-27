namespace ProjectWarehouse.Server.Models.Analytics;

public class ChannelsCancellationsRequest : AnalyticsFilterRequest
{
    /// <summary>Null picks one by the period length, as in the timeseries.</summary>
    public AnalyticsStep? Step { get; init; }
}
