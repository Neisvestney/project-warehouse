using ProjectWarehouse.Server.Infrastructure;

namespace ProjectWarehouse.Server.Domain;

/// <summary>
/// A global auto-mapping rule: a condition on a marketplace card that binds it to a fixed catalog item.
/// Rules are shared by every marketplace account and run before the article and barcode heuristics.
/// </summary>
public class MarketplaceAutoMapRule : IHasIdentity
{
    public Guid Id { get; set; }

    public MarketplaceCardField Field { get; set; }
    public MarketplaceRuleOperator Operator { get; set; }
    public string Value { get; set; } = null!;

    public Guid CatalogItemId { get; set; }
    public CatalogItem CatalogItem { get; set; } = null!;

    public bool IsEnabled { get; set; } = true;

    /// <summary>Higher runs first. The first matching rule wins.</summary>
    public int Priority { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
