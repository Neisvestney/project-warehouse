using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Infrastructure;

namespace ProjectWarehouse.Server.Models.Integrations;

/// <summary>
/// Changelog shape of a manual auto-mapping run: one entry per account whose before/after carry only the
/// cards that changed.
/// </summary>
public class MarketplaceCardMappingsSnapshot : IHasIdentity
{
    public Guid Id { get; init; }
    public IReadOnlyList<MarketplaceCardMappingSnapshotItem> Cards { get; init; } = [];
}

public class MarketplaceCardMappingSnapshotItem : IHasIdentity
{
    public Guid Id { get; init; }
    public string OfferId { get; init; } = null!;
    public Guid? CatalogItemId { get; init; }
    public string? CatalogItemArticle { get; init; }
    public MarketplaceMappingSource? MappingSource { get; init; }
}
