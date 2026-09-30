namespace ProjectWarehouse.Server.Models.Analytics;

/// <summary>The shared filter of the channel reports: the analytics filter narrowed to catalog items.</summary>
public class ChannelsFilterRequest : AnalyticsFilterRequest
{
    /// <summary>
    /// Keeps only the lines of these items: sale lines, returns, accrual lines and Direct box components as they
    /// were ordered, a bundle not unfolded. An order counts when it holds any of them. Empty means all.
    /// </summary>
    public Guid[]? CatalogItemIds { get; init; }
}
