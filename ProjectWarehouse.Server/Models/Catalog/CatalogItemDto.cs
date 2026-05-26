using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Infrastructure;

namespace ProjectWarehouse.Server.Models.Catalog;

public class CatalogItemDto : IHasIdentity
{
    public Guid Id { get; init; }
    public CatalogItemType Type { get; init; }
    public string Name { get; init; } = null!;
    public string FullName { get; init; } = null!;
    public string Article { get; init; } = null!;
    public string? Barcode { get; init; }
    public string? Description { get; init; }
    public string? Notes { get; init; }
    public bool IsArchived { get; init; }
    public Guid? GroupId { get; init; }
    public string? GroupName { get; init; }
    public Guid? SourceBundleId { get; init; }
    public IReadOnlyList<CatalogItemTagDto> Tags { get; init; } = [];
    public IReadOnlyList<BundleComponentDto> Components { get; init; } = [];
    public IReadOnlyList<Guid> VariationIds { get; init; } = [];
    public IReadOnlyList<Guid> MemberIds { get; init; } = [];

    // ProductGroup only
    public IReadOnlyList<CatalogItemDto> Children { get; init; } = [];
}
