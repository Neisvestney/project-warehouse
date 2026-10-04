namespace ProjectWarehouse.Server.Models.Analytics;

/// <summary>Which loss a per-item reasons report counts; both are dated by the order.</summary>
public enum AnalyticsLossKind
{
    /// <summary>Lines of cancelled marketplace orders, each carrying its order's cancel reason.</summary>
    Cancellations = 0,

    /// <summary>Returns of the sales, by the reason of each returned line.</summary>
    Returns = 1,
}
