using ProjectWarehouse.Server.Infrastructure;
using ProjectWarehouse.Server.Models.Files;

namespace ProjectWarehouse.Server.Models.Catalog;

/// <summary>
/// IHasIdentity matters here: the changelog's compare logic matches collection elements by Id
/// rather than by position, so reordering images does not read as a full rewrite.
/// </summary>
public class CatalogItemImageDto : IHasIdentity
{
    public Guid Id { get; init; }
    public DataFileDto File { get; init; } = null!;
    public int Order { get; init; }
}
