using ProjectWarehouse.Server.Domain;

namespace ProjectWarehouse.Server.Models.Analytics;

public class AbcTimelineDto
{
    public string TimeZoneId { get; init; } = null!;
    public AnalyticsAbcBasis Basis { get; init; }
    public AnalyticsAbcSubject Subject { get; init; }

    /// <summary>The currency of the period's analysis, applied to every window.</summary>
    public string? CurrencyCode { get; init; }

    /// <summary>Length of each month's window in days.</summary>
    public int WindowDays { get; init; }

    public bool XyzFromFirstSale { get; init; }
    public AbcAppliedSettingsDto Settings { get; init; } = null!;

    /// <summary>Oldest first; every row carries one cell per month in the same order.</summary>
    public List<AbcTimelineMonthDto> Months { get; init; } = [];

    /// <summary>The items of the table's filters and search, in the period's rank order.</summary>
    public List<AbcTimelineRowDto> Rows { get; init; } = [];
}

public class AbcTimelineMonthDto
{
    /// <summary>First day of the month.</summary>
    public DateOnly Month { get; init; }

    public DateOnly From { get; init; }
    public DateOnly To { get; init; }

    /// <summary>The window ends before the month does: the month is not over yet or the period ends inside it.</summary>
    public bool IsPartial { get; init; }

    /// <summary>Full XYZ intervals of the window; below <see cref="AbcAppliedSettingsDto.XyzMinIntervals"/> no cell has an XYZ class.</summary>
    public int XyzIntervals { get; init; }
}

public class AbcTimelineRowDto
{
    /// <summary>Place in the period's analysis, 1-based.</summary>
    public int Rank { get; init; }

    public AbcSubjectDto Subject { get; init; } = null!;

    public List<AbcTimelineCellDto> Cells { get; init; } = [];
}

/// <summary>All null when the item sold nothing in the window.</summary>
public class AbcTimelineCellDto
{
    public AbcClass? AbcClass { get; init; }

    /// <summary>Share of the window's value.</summary>
    public double? Share { get; init; }

    public XyzClass? XyzClass { get; init; }
    public double? Cv { get; init; }
}
