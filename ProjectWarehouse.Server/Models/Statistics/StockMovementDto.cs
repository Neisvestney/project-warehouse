using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Models.Catalog;

namespace ProjectWarehouse.Server.Models.Statistics;

/// <summary>A single journal row, for drilling into a day of the chart.</summary>
public class StockMovementDto
{
    public Guid Id { get; init; }
    public DateTime CreatedAt { get; init; }
    public StockMovementDirection Direction { get; init; }
    public string Action { get; init; } = null!;
    public int Quantity { get; init; }

    public Guid CatalogItemId { get; init; }
    public CatalogItemSummaryDto CatalogItem { get; init; } = null!;

    public Guid? WarehouseId { get; init; }
    public string? WarehouseName { get; init; }
    public Guid? StoragePlaceId { get; init; }
    public string? StoragePlaceName { get; init; }
    public Guid? StoragePlaceNodeId { get; init; }
    public string? StoragePlaceNodeName { get; init; }

    public Guid? UserId { get; init; }
    public string? UserName { get; init; }
}
