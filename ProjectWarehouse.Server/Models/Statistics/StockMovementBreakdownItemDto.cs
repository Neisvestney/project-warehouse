namespace ProjectWarehouse.Server.Models.Statistics;

/// <summary>One row of a grouped report. <see cref="Key"/> is null when grouping by action, and also
/// for rows whose referenced entity has since been deleted.</summary>
public class StockMovementBreakdownItemDto : StockMovementTotalsDto
{
    public Guid? Key { get; init; }
    public string? Label { get; init; }
}
