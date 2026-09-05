using System.ComponentModel.DataAnnotations;
using ProjectWarehouse.Server.Domain;

namespace ProjectWarehouse.Server.Models.Catalog;

public class ProductGroupChildRequest
{
    public Guid? Id { get; init; }

    public CatalogItemType Type { get; init; }

    [Required, MinLength(1)]
    public string Name { get; init; } = null!;

    [Required, MinLength(1)]
    public string Article { get; init; } = null!;

    public string? Barcode { get; init; }
    public string? Description { get; init; }
    public string? Notes { get; init; }
    public string? LabelText { get; init; }
    public bool IsArchived { get; init; }

    public IReadOnlyList<Guid> Tags { get; init; } = [];

    public Guid? MainImageFileId { get; init; }
    public IReadOnlyList<CatalogItemImageRequest> Images { get; init; } = [];
}
