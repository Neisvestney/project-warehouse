using ProjectWarehouse.Server.Domain;

namespace ProjectWarehouse.Server.Models.Analytics;

public class ChannelsWeekdaysDto
{
    public DateOnly From { get; init; }
    public DateOnly To { get; init; }
    public string TimeZoneId { get; init; } = null!;
    public AnalyticsMeasure Measure { get; init; }
    public WeekdayScale Scale { get; init; }

    /// <summary>Last day averaged, yesterday at the latest: an unfinished day would drag the averages down.</summary>
    public DateOnly? CountedTo { get; init; }

    /// <summary>Full Monday–Sunday weeks between <c>From</c> and <c>CountedTo</c>; only <c>WeekShare</c> uses them.</summary>
    public int FullWeeks { get; init; }

    /// <summary>Every selected channel together, Monday first.</summary>
    public List<decimal?> Total { get; init; } = [];

    /// <summary>
    /// One per shop, then the whole Direct channel, one per tag met in the period and the untagged; tag rows
    /// overlap, as in the summary.
    /// </summary>
    public List<WeekdayRowDto> Rows { get; init; } = [];
}

public class WeekdayRowDto
{
    public AnalyticsChannelKind Kind { get; init; }
    public Guid? MarketplaceAccountId { get; init; }
    public MarketplaceType? MarketplaceType { get; init; }
    public Guid? TagId { get; init; }

    /// <summary>Shop or tag name; null for the whole Direct channel and the untagged row.</summary>
    public string? Name { get; init; }

    /// <summary>
    /// Monday first. <c>Average</c>: per occurrence of the weekday, null for a weekday the period lacks.
    /// <c>WeekShare</c>: median share of the week, 0…1, null when the row has no full week with sales.
    /// </summary>
    public List<decimal?> Values { get; init; } = [];
}
