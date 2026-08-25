using ProjectWarehouse.Server.Models.Catalog;

namespace ProjectWarehouse.Server.Models.Forecast;

/// <summary>
/// One forecast unit. Deliberately carries no catalog item: on a stock row and in the item drawer the
/// item is already on screen, and repeating it there would double the payload for one use site.
/// </summary>
public class StockForecastDto
{
    public Guid CatalogItemId { get; init; }

    public int Stock { get; init; }

    /// <summary>Average per day over the window, rounded to two decimals.</summary>
    public decimal DailyConsumption { get; init; }

    public int ConsumedInWindow { get; init; }

    /// <summary>Null is "never runs out", not "no data" — it sorts last and never counts as a warning.</summary>
    public int? DaysLeft { get; init; }

    public int WarningDays { get; init; }

    /// <summary>True when the threshold came from the item's own override rather than the warehouse.</summary>
    public bool IsWarningOverridden { get; init; }

    public StockForecastStatus Status { get; init; }
}

/// <summary>A forecast row of the list, where the item does have to travel with the numbers.</summary>
public class StockForecastRowDto : StockForecastDto
{
    public CatalogItemSummaryDto CatalogItem { get; init; } = null!;
}

/// <summary>
/// The page plus the settings it was computed under — window, averaging mode and time zone are read
/// from the warehouse, so the client can only label them if they come back with the result.
/// </summary>
public class StockForecastListDto
{
    public Paginated<StockForecastRowDto> Items { get; init; } = new();
    public int WindowDays { get; init; }
    public bool UseWeightedConsumption { get; init; }
    public string TimeZoneId { get; init; } = null!;

    /// <summary>Threshold every row without an override inherited.</summary>
    public int WarehouseWarningDays { get; init; }
}
