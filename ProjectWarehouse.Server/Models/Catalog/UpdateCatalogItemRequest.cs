using System.ComponentModel.DataAnnotations;

namespace ProjectWarehouse.Server.Models.Catalog;

public class UpdateCatalogItemRequest
{
    [Required, MinLength(1)]
    public string Name { get; init; } = null!;

    [Required, MinLength(1)]
    public string Article { get; init; } = null!;

    public string? Barcode { get; init; }
    public string? Description { get; init; }
    public string? Notes { get; init; }
    public bool IsArchived { get; init; }

    public IReadOnlyList<Guid> Tags { get; init; } = [];

    public Guid? MainImageFileId { get; init; }
    public IReadOnlyList<CatalogItemImageRequest> Images { get; init; } = [];

    // Standard / Unit only
    public Guid? GroupId { get; init; }

    // Variation only
    public IReadOnlyList<Guid> MemberIds { get; init; } = [];

    // Bundle only
    public IReadOnlyList<BundleComponentRequest> Components { get; init; } = [];

    // ProductGroup only
    public IReadOnlyList<ProductGroupChildRequest> Children { get; init; } = [];
}
