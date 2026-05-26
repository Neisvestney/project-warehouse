using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Infrastructure;

namespace ProjectWarehouse.Server.Models.Catalog;

public class CatalogItemSummaryDto : IHasIdentity
{
    public Guid Id { get; init; }
    public CatalogItemType Type { get; init; }
    public string Name { get; init; } = null!;
    public string FullName { get; init; } = null!;
    public string Article { get; init; } = null!;
    public string? Barcode { get; init; }
    public bool IsArchived { get; init; }
    public IReadOnlyList<CatalogItemTagDto> Tags { get; init; } = [];
}
