using System.ComponentModel.DataAnnotations;

namespace ProjectWarehouse.Server.Models.Catalog;

public class CreateCatalogItemTagRequest
{
    [Required, MaxLength(100)]
    public string Name { get; init; } = null!;
}
