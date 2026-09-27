using ProjectWarehouse.Server.Domain;

namespace ProjectWarehouse.Server.Models.Analytics;

public class ChannelsWeekdaysDto
{
    public DateOnly From { get; init; }
    public DateOnly To { get; init; }
    public string TimeZoneId { get; init; } = null!;
    public AnalyticsMeasure Measure { get; init; }

    /// <summary>Last day averaged, yesterday at the latest: an unfinished day would drag the averages down.</summary>
    public DateOnly? CountedTo { get; init; }

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

    /// <summary>Average per occurrence of the weekday, Monday first; null for a weekday the period lacks.</summary>
    public List<decimal?> Values { get; init; } = [];
}
