namespace ProjectWarehouse.Server.Models.Warehouses;

public class NodeCatalogItemDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = null!;
    public string Article { get; init; } = null!;
    public string? Barcode { get; init; }
}
