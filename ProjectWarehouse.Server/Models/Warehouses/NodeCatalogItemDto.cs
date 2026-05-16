using ProjectWarehouse.Server.Infrastructure;

namespace ProjectWarehouse.Server.Models.Warehouses;

public class NodeCatalogItemDto : IHasIdentity
{
    public Guid Id { get; init; }
    public string Name { get; init; } = null!;
    public string Article { get; init; } = null!;
    public string? Barcode { get; init; }
}
