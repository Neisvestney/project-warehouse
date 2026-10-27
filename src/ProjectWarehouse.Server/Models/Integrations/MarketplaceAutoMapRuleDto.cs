using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Infrastructure;

namespace ProjectWarehouse.Server.Models.Integrations;

public class MarketplaceAutoMapRuleDto : IHasIdentity
{
    public Guid Id { get; init; }
    public MarketplaceCardField Field { get; init; }
    public MarketplaceRuleOperator Operator { get; init; }
    public string Value { get; init; } = null!;

    public Guid CatalogItemId { get; init; }
    public string CatalogItemFullName { get; init; } = null!;
    public string CatalogItemArticle { get; init; } = null!;

    /// <summary>The target is archived. Informational only — the rule still applies.</summary>
    public bool IsTargetArchived { get; init; }

    public bool IsEnabled { get; init; }
    public int Priority { get; init; }

    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}
