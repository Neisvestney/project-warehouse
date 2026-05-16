using ProjectWarehouse.Server.Infrastructure;

namespace ProjectWarehouse.Server.Models.Catalog;

public class CatalogItemSummaryDto : IHasIdentity
{
    public Guid Id { get; init; }
    public string Name { get; init; } = null!;
    public string Article { get; init; } = null!;
    public string? Barcode { get; init; }
    public int CharacteristicCount { get; init; }
}