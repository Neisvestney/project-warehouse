using System.ComponentModel.DataAnnotations;

namespace ProjectWarehouse.Server.Models.Analytics;

/// <summary>The channel is narrowed by the shared filter itself, as in the top items; Direct takes no part.</summary>
public class ChannelsLossReasonsRequest : ChannelsFilterRequest
{
    public AnalyticsLossKind Kind { get; init; } = AnalyticsLossKind.Returns;

    public AnalyticsAbcSubject Subject { get; init; } = AnalyticsAbcSubject.CatalogItem;

    public AnalyticsLossReasonsBy By { get; init; } = AnalyticsLossReasonsBy.Units;

    /// <summary>Null picks one by the period length, as in the timeseries.</summary>
    public AnalyticsStep? Step { get; init; }

    /// <summary>Null lists every ranked row.</summary>
    [Range(1, 1000)]
    public int? Take { get; init; }
}
