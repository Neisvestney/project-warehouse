using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Infrastructure;
using ProjectWarehouse.Server.Models.Files;
using ProjectWarehouse.Server.Models.Integrations;

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
    public IReadOnlyList<CatalogItemTagDto> Tags { get; init; } = [];
    public IReadOnlyList<BundleComponentDto> Components { get; init; } = [];
    public IReadOnlyList<Guid> VariationIds { get; init; } = [];
    public IReadOnlyList<Guid> MemberIds { get; init; } = [];
    public IReadOnlyList<MarketplaceAccountShortSummaryDto> MarketplaceAccounts { get; init; } = [];

    /// <summary>The item's own main image, absent when the image shown is inherited from the group.</summary>
    public Guid? MainImageFileId { get; init; }

    /// <summary>Effective main image: the item's own, otherwise the group's.</summary>
    public DataFileDto? MainImage { get; init; }

    /// <summary>Additional images. Unlike <see cref="MainImage"/> these are never inherited.</summary>
    public IReadOnlyList<CatalogItemImageDto> Images { get; init; } = [];

    // ProductGroup only
    public IReadOnlyList<CatalogItemDto> Children { get; init; } = [];
}
