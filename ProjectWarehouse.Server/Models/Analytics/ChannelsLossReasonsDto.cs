namespace ProjectWarehouse.Server.Models.Analytics;

public class ChannelsLossReasonsDto
{
    public DateOnly From { get; init; }
    public DateOnly To { get; init; }
    public string TimeZoneId { get; init; } = null!;
    public AnalyticsLossKind Kind { get; init; }
    public AnalyticsAbcSubject Subject { get; init; }
    public AnalyticsLossReasonsBy By { get; init; }
    public AnalyticsStep Step { get; init; }
    public List<AnalyticsIntervalDto> Intervals { get; init; } = [];

    /// <summary>Per interval: its returns are still coming in. All false for cancellations.</summary>
    public List<bool> ImmatureIntervals { get; init; } = [];

    public int ReturnsMaturityDays { get; init; }

    /// <summary>The base a subject needs to be ranked by share.</summary>
    public int MinShareBase { get; init; }

    /// <summary>Lost units of every subject, the ones without a subject excluded.</summary>
    public int TotalUnits { get; init; }

    /// <summary>Rows in the ranking before <c>take</c> cuts the list.</summary>
    public int TotalRows { get; init; }

    /// <summary>Lost units with no catalog item or card for the subject: counted nowhere in the ranking.</summary>
    public int UnlinkedUnits { get; init; }

    public List<LossReasonRowDto> Rows { get; init; } = [];
}

/// <summary>One subject and one reason.</summary>
public class LossReasonRowDto
{
    public AbcSubjectDto Subject { get; init; } = null!;

    /// <summary>Null when the marketplace gave none.</summary>
    public string? Reason { get; init; }

    public int Units { get; init; }

    /// <summary>
    /// Units the share is taken of: the subject's sold units for returns, sold plus cancelled for cancellations.
    /// </summary>
    public int BaseUnits { get; init; }

    /// <summary><see cref="Units"/> over <see cref="BaseUnits"/>; null on a zero base.</summary>
    public double? Share { get; init; }

    /// <summary>Lost units by the order's interval; null for an interval after today.</summary>
    public List<int?> Values { get; init; } = [];
}
