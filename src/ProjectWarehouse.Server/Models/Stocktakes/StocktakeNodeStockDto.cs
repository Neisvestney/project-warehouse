using ProjectWarehouse.Server.Models.Catalog;

namespace ProjectWarehouse.Server.Models.Stocktakes;

/// <summary>
/// Live stock of one node in the scope, used to pre-populate the counting screen. Standard goods and
/// serials come in one response so the accordion can render without a second round trip.
/// </summary>
public class StocktakeNodeStockDto
{
    public Guid StoragePlaceNodeId { get; init; }
    public string[] NodePath { get; init; } = [];
    public IReadOnlyList<StocktakeNodeStandardStockDto> Standard { get; init; } = [];
    public IReadOnlyList<StocktakeNodeUnitStockDto> Units { get; init; } = [];
}

public class StocktakeNodeStandardStockDto
{
    public Guid CatalogItemId { get; init; }
    public CatalogItemSummaryDto? CatalogItem { get; init; }
    public string CatalogItemName { get; init; } = null!;
    public int Expected { get; init; }
}

public class StocktakeNodeUnitStockDto
{
    public Guid UnitInventoryItemId { get; init; }
    public string InventoryNumber { get; init; } = null!;
    public Guid CatalogItemId { get; init; }
    public CatalogItemSummaryDto? CatalogItem { get; init; }
    public string CatalogItemName { get; init; } = null!;
}
