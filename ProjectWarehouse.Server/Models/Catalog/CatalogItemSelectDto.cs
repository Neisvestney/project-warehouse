using ProjectWarehouse.Server.Domain;

namespace ProjectWarehouse.Server.Models.Catalog;

public class CatalogItemSelectDto
{
    public Guid Id { get; init; }
    public CatalogItemType Type { get; init; }
    public string Name { get; init; } = null!;
    public string FullName { get; init; } = null!;
    public string Article { get; init; } = null!;
}
