namespace ProjectWarehouse.Server.Models.Analytics;

public class ChannelsLossesRequest : ChannelsFilterRequest
{
    /// <summary>Null picks one by the period length, as in the timeseries.</summary>
    public AnalyticsStep? Step { get; init; }

    /// <summary>What <see cref="LossesPointDto.Sales"/> counts; the shares do not depend on it.</summary>
    public AnalyticsMeasure Measure { get; init; } = AnalyticsMeasure.Orders;
}
