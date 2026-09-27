namespace ProjectWarehouse.Server.Models.Analytics;

public class ChannelsTimeseriesDto
{
    public DateOnly From { get; init; }
    public DateOnly To { get; init; }
    public string TimeZoneId { get; init; } = null!;

    /// <summary>The step applied — the requested one, or the one picked by the period length.</summary>
    public AnalyticsStep Step { get; init; }

    public AnalyticsMeasure Measure { get; init; }

    public List<AnalyticsIntervalDto> Intervals { get; init; } = [];

    /// <summary>One per shop, then the whole Direct channel; Direct tag rows are not split out here.</summary>
    public List<ChannelSeriesDto> Series { get; init; } = [];
}
