namespace ProjectWarehouse.Server.Models.Catalog;

public class CatalogItemDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = null!;
    public string Article { get; init; } = null!;
    public string? Barcode { get; init; }
    public IReadOnlyList<CatalogItemCharacteristicDto> Characteristics { get; init; } = [];
}