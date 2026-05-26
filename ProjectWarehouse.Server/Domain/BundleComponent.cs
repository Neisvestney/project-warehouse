using ProjectWarehouse.Server.Infrastructure;

namespace ProjectWarehouse.Server.Domain;

public class BundleComponent : IHasIdentity
{
    public Guid Id { get; set; }

    public Guid BundleId { get; set; }
    public CatalogItem Bundle { get; set; } = null!;

    public Guid ComponentId { get; set; }
    public CatalogItem Component { get; set; } = null!;

    public int Quantity { get; set; }
}
