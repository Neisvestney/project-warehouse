using ProjectWarehouse.Server.Infrastructure;

namespace ProjectWarehouse.Server.Domain;

public abstract class ItemsGroup : IHasIdentity
{
    public Guid Id { get; set; }
    public Guid CatalogItemId { get; set; }
    public CatalogItem CatalogItem { get; set; } = null!;
    public int Count { get; set; }
}
