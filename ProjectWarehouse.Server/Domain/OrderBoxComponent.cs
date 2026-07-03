using ProjectWarehouse.Server.Infrastructure;

namespace ProjectWarehouse.Server.Domain;

public class OrderBoxComponent : IHasIdentity
{
    public Guid Id { get; set; }

    public Guid OrderBoxId { get; set; }
    public OrderBox OrderBox { get; set; } = null!;

    public Guid CatalogItemId { get; set; }
    public CatalogItem CatalogItem { get; set; } = null!;

    public int Quantity { get; set; }
}
