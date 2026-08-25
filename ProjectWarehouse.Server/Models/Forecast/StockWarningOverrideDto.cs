namespace ProjectWarehouse.Server.Models.Forecast;

/// <summary>
/// Shape the per-item threshold is journalled in. The catalog item's own read DTO does not carry a
/// per-warehouse threshold, so the changelog diffs this pair instead; a null <see cref="WarningDays"/>
/// on either side means the item inherits the warehouse setting.
/// </summary>
public class StockWarningOverrideDto
{
    public Guid CatalogItemId { get; init; }
    public Guid WarehouseId { get; init; }
    public string WarehouseName { get; init; } = null!;
    public int? WarningDays { get; init; }
}
