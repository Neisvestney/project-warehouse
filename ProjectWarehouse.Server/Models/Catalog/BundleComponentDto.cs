using ProjectWarehouse.Server.Domain;

namespace ProjectWarehouse.Server.Models.Catalog;

public class BundleComponentDto
{
    public Guid Id { get; init; }
    public Guid ComponentId { get; init; }
    public string ComponentName { get; init; } = null!;
    public CatalogItemType ComponentType { get; init; }
    public int Quantity { get; init; }
}
