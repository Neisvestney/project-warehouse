using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Infrastructure;
using ProjectWarehouse.Server.Models.Files;

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

    /// <summary>Effective main image: the item's own, otherwise the group's.</summary>
    public DataFileDto? MainImage { get; init; }
}
