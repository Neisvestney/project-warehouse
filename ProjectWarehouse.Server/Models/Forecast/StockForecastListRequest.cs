using System.ComponentModel.DataAnnotations;
using ProjectWarehouse.Server.Domain;

namespace ProjectWarehouse.Server.Models.Forecast;

/// <summary>
/// Filters of the forecast page. Window, averaging mode and time zone are absent on purpose: they are
/// warehouse settings, not page filters, so the number reads the same for everyone looking at it.
/// </summary>
public class StockForecastListRequest
{
    /// <summary>
    /// Required: consumption differs per warehouse, and an average across all of them means nothing.
    /// </summary>
    [Required]
    public Guid? WarehouseId { get; init; }

    public string? SearchString { get; init; }

    /// <summary>Physical types to keep. Empty means both <c>Standard</c> and <c>Unit</c>.</summary>
    public CatalogItemType[]? CatalogItemTypes { get; init; }

    public Guid[]? TagIds { get; init; }

    public bool? IsArchived { get; init; }

    /// <summary>Leaves only <c>OutOfStock</c> and <c>Warning</c>.</summary>
    public bool OnlyWarnings { get; init; }

    /// <summary>
    /// Reserves the not-yet-fulfilled quantity of items on orders currently in <c>Assembly</c> against
    /// stock, as if it were already spoken for. A Bundle component is exploded into its own components
    /// recursively; a Variation component is dropped, since it has no single deterministic underlying item.
    /// </summary>
    public bool AccountForAssembly { get; init; }

    public StockForecastSortBy SortBy { get; init; } = StockForecastSortBy.Default;

    public SortOrder SortOrder { get; init; } = SortOrder.Asc;
}
