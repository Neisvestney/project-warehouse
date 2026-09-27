namespace ProjectWarehouse.Server.Models.Analytics;

/// <summary>Cancellations of the shops per interval, by the order date — the same orders the summary counts.</summary>
public class ChannelsCancellationsDto
{
    public DateOnly From { get; init; }
    public DateOnly To { get; init; }
    public string TimeZoneId { get; init; } = null!;

    public AnalyticsStep Step { get; init; }
    public List<AnalyticsIntervalDto> Intervals { get; init; } = [];

    /// <summary>Cancelled orders, one per shop.</summary>
    public List<ChannelSeriesDto> Series { get; init; } = [];

    /// <summary>Sales plus cancellations, in the same order as <see cref="Series"/>: the base of the cancellation share.</summary>
    public List<ChannelSeriesDto> Orders { get; init; } = [];
}
