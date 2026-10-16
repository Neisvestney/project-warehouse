using ProjectWarehouse.Server.Domain;

namespace ProjectWarehouse.Server.Models.Analytics;

/// <summary>One step of a series, inclusive bounds clamped to the period.</summary>
public class AnalyticsIntervalDto
{
    public DateOnly Start { get; init; }
    public DateOnly End { get; init; }

    /// <summary>The step sticks out of the period on one side, so the interval holds fewer days than its peers.</summary>
    public bool IsPartial { get; init; }

    /// <summary>Contains today: the number is still growing.</summary>
    public bool IsCurrent { get; init; }

    /// <summary>Starts after today; its values are null rather than zero.</summary>
    public bool IsFuture { get; init; }
}

public class ChannelSeriesDto
{
    public AnalyticsChannelKind Kind { get; init; }
    public Guid? MarketplaceAccountId { get; init; }
    public MarketplaceType? MarketplaceType { get; init; }

    /// <summary>Shop name; null for the Direct channel.</summary>
    public string? Name { get; init; }

    /// <summary>One value per interval, in the same order; null for a future interval.</summary>
    public List<int?> Values { get; init; } = [];

    public int Total { get; init; }
}
