namespace ProjectWarehouse.Server.Domain;

/// <summary>
/// Per-item warning threshold on one warehouse. Only the threshold is overridable: the window and the
/// averaging mode describe how demand behaves on a warehouse, not a property of a position, and
/// different windows on neighbouring rows would make the consumption column incomparable.
/// A missing row means "inherit from the warehouse" — resetting an override deletes the row.
/// </summary>
public class CatalogItemStockWarning
{
    public Guid CatalogItemId { get; set; }
    public CatalogItem CatalogItem { get; set; } = null!;

    public Guid WarehouseId { get; set; }
    public Warehouse Warehouse { get; set; } = null!;

    public int WarningDays { get; set; }
}
