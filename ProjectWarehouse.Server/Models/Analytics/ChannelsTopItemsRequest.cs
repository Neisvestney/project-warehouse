using System.ComponentModel.DataAnnotations;

namespace ProjectWarehouse.Server.Models.Analytics;

/// <summary>The channel is narrowed by the shared filter itself: one shop, Direct alone, or everything selected.</summary>
public class ChannelsTopItemsRequest : AnalyticsFilterRequest
{
    public AnalyticsTopItemsBy By { get; init; } = AnalyticsTopItemsBy.Units;

    public AnalyticsMoneyMode MoneyMode { get; init; } = AnalyticsMoneyMode.Price;

    /// <summary>Currency of the money column; null takes the one with the most sale lines.</summary>
    public string? CurrencyCode { get; init; }

    /// <summary>Null lists every ranked item.</summary>
    [Range(1, 1000)]
    public int? Take { get; init; }
}
