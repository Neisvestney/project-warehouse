using ProjectWarehouse.Server.Infrastructure;

namespace ProjectWarehouse.Server.Domain;

public class BundleComponent : IHasIdentity
{
    public Guid Id { get; set; }

    public Guid BundleId { get; set; }
    public CatalogItem Bundle { get; set; } = null!;

    public Guid ComponentId { get; set; }
    public CatalogItem Component { get; set; } = null!;

    /// <summary>Position of the component within the bundle, ascending.</summary>
    public int Order { get; set; }

    public int Quantity { get; set; }
}
