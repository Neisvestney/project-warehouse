using System.ComponentModel.DataAnnotations;
using ProjectWarehouse.Server.Infrastructure.Files;

namespace ProjectWarehouse.Server.Models.Catalog;

public class CatalogItemImageRequest : IDataFileLinkRequest
{
    /// <summary>Null for an image not yet attached to the item.</summary>
    public Guid? Id { get; init; }

    [Required]
    public Guid FileId { get; init; }

    public int Order { get; init; }
}
