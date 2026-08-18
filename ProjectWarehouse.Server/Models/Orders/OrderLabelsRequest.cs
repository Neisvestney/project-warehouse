namespace ProjectWarehouse.Server.Models.Orders;

/// <summary>How the merged label stack is ordered.</summary>
public enum OrderLabelsGrouping
{
    /// <summary>The order the caller listed — the printed stack matches the list on screen.</summary>
    None = 0,

    /// <summary>Orders with the same set of articles print back to back.</summary>
    Article = 1,
}

public class OrderLabelsRequest
{
    public IReadOnlyList<Guid> OrderIds { get; init; } = [];

    public OrderLabelsGrouping Grouping { get; init; } = OrderLabelsGrouping.None;
}
