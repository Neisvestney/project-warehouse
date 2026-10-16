using ProjectWarehouse.Server.Domain;

namespace ProjectWarehouse.Server.Models.Orders;

public class OrderBoxComponentDto
{
    public Guid Id { get; init; }
    public Guid CatalogItemId { get; init; }
    public string CatalogItemName { get; init; } = null!;
    public CatalogItemType CatalogItemType { get; init; }
    public int Quantity { get; init; }
}
