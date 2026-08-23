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

    /// <summary>The target was archived after the rule was created — the rule is skipped until it is fixed.</summary>
    public bool IsTargetArchived { get; init; }

    public bool IsEnabled { get; init; }
    public int Priority { get; init; }

    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}
