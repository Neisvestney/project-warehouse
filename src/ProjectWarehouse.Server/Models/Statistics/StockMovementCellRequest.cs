namespace ProjectWarehouse.Server.Models.Statistics;

/// <summary>
/// The movements behind one pivot cell: the shared filter narrowed to the cell's day and item, plus the
/// predicate of the metric the cell belongs to. A POST for the same reason as the pivot itself.
/// </summary>
public class StockMovementCellRequest : StockMovementFilterRequest
{
    /// <summary>The cell's metric; null for the net and balance columns, which cover every movement.</summary>
    public StockMovementMetricDto? Metric { get; init; }

    /// <summary>Paging cursor: the previous page's <c>nextBefore</c>, or null for the first page.</summary>
    public DateTime? Before { get; init; }
}
