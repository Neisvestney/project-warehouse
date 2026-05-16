using ProjectWarehouse.Server.Infrastructure;

namespace ProjectWarehouse.Server.Models.Catalog;

public class CatalogItemDto : IHasIdentity
{
    public Guid Id { get; init; }
    public string Name { get; init; } = null!;
    public string Article { get; init; } = null!;
    public string? Barcode { get; init; }
    public IReadOnlyList<CatalogItemCharacteristicDto> Characteristics { get; init; } = [];
}