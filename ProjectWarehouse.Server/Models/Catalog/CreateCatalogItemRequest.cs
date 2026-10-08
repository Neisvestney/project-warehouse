using System.ComponentModel.DataAnnotations;
using ProjectWarehouse.Server.Domain;

namespace ProjectWarehouse.Server.Models.Catalog;

public class CreateCatalogItemRequest
{
    [Required]
    public CatalogItemType Type { get; init; }

    [Required, MinLength(1)]
    public string Name { get; init; } = null!;

    [Required, MinLength(1)]
    public string Article { get; init; } = null!;

    public string? Barcode { get; init; }

    /// <summary>GTIN-8/12/13/14; stored padded to 14 digits.</summary>
    public string? Gtin { get; init; }

    /// <summary>Additional images are edited afterwards in the item drawer.</summary>
    public Guid? MainImageFileId { get; init; }
}
