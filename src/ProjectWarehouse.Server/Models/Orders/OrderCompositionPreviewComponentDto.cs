namespace ProjectWarehouse.Server.Models.Orders;

public class OrderCompositionPreviewComponentDto
{
    public Guid CatalogItemId { get; init; }
    public string CatalogItemName { get; init; } = null!;
    public int Quantity { get; init; }
}
