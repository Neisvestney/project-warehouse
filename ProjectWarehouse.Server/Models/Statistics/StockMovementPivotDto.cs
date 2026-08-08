using ProjectWarehouse.Server.Models.Catalog;

namespace ProjectWarehouse.Server.Models.Statistics;

/// <summary>A column of the pivot — one catalog item, with its totals for the whole range.</summary>
public class StockMovementPivotColumnDto : StockMovementTotalsDto
{
    public Guid CatalogItemId { get; init; }
    public CatalogItemSummaryDto CatalogItem { get; init; } = null!;
}

public class StockMovementPivotCellDto : StockMovementTotalsDto
{
    public Guid CatalogItemId { get; init; }
}

/// <summary>
/// One day. <see cref="Cells"/> is sparse — days where an item did not move carry no cell at all;
/// <see cref="Total"/> covers every item matching the filter, including ones cut from the columns.
/// </summary>
public class StockMovementPivotRowDto
{
    public DateOnly Date { get; init; }
    public IReadOnlyList<StockMovementPivotCellDto> Cells { get; init; } = [];
    public StockMovementTotalsDto Total { get; init; } = new();
}

/// <summary>Dates down, catalog items across.</summary>
public class StockMovementPivotDto
{
    public DateOnly From { get; init; }
    public DateOnly To { get; init; }

    /// <summary>Ordered by total quantity moved, descending.</summary>
    public IReadOnlyList<StockMovementPivotColumnDto> Columns { get; init; } = [];

    /// <summary>One entry per day of the range, empty days included.</summary>
    public IReadOnlyList<StockMovementPivotRowDto> Rows { get; init; } = [];

    public StockMovementTotalsDto Totals { get; init; } = new();

    /// <summary>True when items were left out because the column limit was reached.</summary>
    public bool HasMoreColumns { get; init; }
}
