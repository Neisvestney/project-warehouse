namespace ProjectWarehouse.Server.Domain;

/// <summary>
/// One sub-column of the stock movement report: a named filter over the journal. An empty predicate is
/// legal and means «every movement». Metrics may overlap — «Новый товар» covers «Приёмка HOT» — because
/// the fixed Net and Balance columns are computed from raw directions, never by summing metrics.
/// </summary>
public class StockMovementMetric
{
    public string Name { get; set; } = null!;
    public string[]? Actions { get; set; }
    public StockMovementDirection[]? Directions { get; set; }
    public Guid[]? ReceiptTagIds { get; set; }
}
