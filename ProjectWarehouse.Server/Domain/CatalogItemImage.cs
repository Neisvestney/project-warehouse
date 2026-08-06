using ProjectWarehouse.Server.Infrastructure.Files;

namespace ProjectWarehouse.Server.Domain;

/// <summary>Additional catalog item image. The main image is a direct FK on <see cref="CatalogItem"/>.</summary>
public class CatalogItemImage : IDataFileLink
{
    public Guid Id { get; set; }

    public Guid CatalogItemId { get; set; }
    public CatalogItem CatalogItem { get; set; } = null!;

    public Guid DataFileId { get; set; }
    public DataFile DataFile { get; set; } = null!;

    public int Order { get; set; }
}
