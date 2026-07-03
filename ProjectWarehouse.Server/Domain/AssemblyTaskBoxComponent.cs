using ProjectWarehouse.Server.Infrastructure;

namespace ProjectWarehouse.Server.Domain;

public class AssemblyTaskBoxComponent : IHasIdentity
{
    public Guid Id { get; set; }

    public Guid AssemblyTaskBoxId { get; set; }
    public AssemblyTaskBox AssemblyTaskBox { get; set; } = null!;

    public Guid CatalogItemId { get; set; }
    public CatalogItem CatalogItem { get; set; } = null!;

    public int Quantity { get; set; }

    public ICollection<AssemblyFulfillment> Fulfillments { get; set; } = [];
}
