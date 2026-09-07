using System.ComponentModel.DataAnnotations;

namespace ProjectWarehouse.Server.Models.Catalog;

public class CatalogItemsByIdsRequest
{
    [Required]
    public IReadOnlyList<Guid> Ids { get; init; } = [];
}
