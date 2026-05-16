using ProjectWarehouse.Server.Infrastructure;

namespace ProjectWarehouse.Server.Models.Warehouses;

public class NodeCharacteristicDto : IHasIdentity
{
    public Guid Id { get; init; }
    public string Characteristic { get; init; } = null!;
    public string? Barcode { get; init; }
    public NodeCatalogItemDto CatalogItem { get; init; } = null!;
}
