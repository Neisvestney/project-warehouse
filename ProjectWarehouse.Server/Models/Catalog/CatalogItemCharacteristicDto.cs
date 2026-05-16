using ProjectWarehouse.Server.Infrastructure;

namespace ProjectWarehouse.Server.Models.Catalog;

public class CatalogItemCharacteristicDto : IHasIdentity
{
    public Guid Id { get; init; }
    public string Characteristic { get; init; } = null!;
    public string? Barcode { get; init; }
}