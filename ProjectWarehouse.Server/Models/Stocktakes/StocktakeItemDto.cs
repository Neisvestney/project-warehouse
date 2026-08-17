using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Models.Catalog;

namespace ProjectWarehouse.Server.Models.Stocktakes;

public class StocktakeItemDto
{
    public Guid Id { get; init; }
    public StocktakeItemKind Kind { get; init; }
    public Guid CatalogItemId { get; init; }
    public CatalogItemSummaryDto? CatalogItem { get; init; }
    public string CatalogItemName { get; init; } = null!;
    public int CountedQuantity { get; init; }
    public string? InventoryNumber { get; init; }
    public Guid? UnitInventoryItemId { get; init; }
    public string? Notes { get; init; }

    /// <summary>Stock change applied when the document was finished. Null until then.</summary>
    public int? AppliedDelta { get; init; }
}
