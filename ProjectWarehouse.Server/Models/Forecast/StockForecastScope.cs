using ProjectWarehouse.Server.Domain;

namespace ProjectWarehouse.Server.Models.Forecast;

/// <summary>
/// Narrows what a system-side computation returns. No paging: the set is already cut down by status,
/// and slicing a notification into pages means nothing.
/// </summary>
public class StockForecastScope
{
    /// <summary>Keep only <c>OutOfStock</c> and <c>Warning</c>.</summary>
    public bool OnlyWarnings { get; init; }

    /// <summary>Physical types to keep. Empty means both.</summary>
    public IReadOnlyList<CatalogItemType>? CatalogItemTypes { get; init; }

    public bool ExcludeArchived { get; init; }
}
