namespace ProjectWarehouse.Server.Domain;

/// <summary>
/// The tag subtype a management request addresses. Mirrors the <c>Tag</c> discriminator: every value here
/// has exactly one <see cref="Tag"/> descendant behind it.
/// </summary>
public enum TagKind
{
    Receipt,
    CatalogItem,
}
