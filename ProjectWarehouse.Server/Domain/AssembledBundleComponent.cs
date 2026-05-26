using ProjectWarehouse.Server.Infrastructure;

namespace ProjectWarehouse.Server.Domain;

public class AssembledBundleComponent : IHasIdentity
{
    public Guid Id { get; set; }

    public Guid AssembledBundleId { get; set; }
    public CatalogItem AssembledBundle { get; set; } = null!;

    public Guid ComponentId { get; set; }
    public CatalogItem Component { get; set; } = null!;

    public int Quantity { get; set; }
}
