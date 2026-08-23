using System.ComponentModel.DataAnnotations;
using ProjectWarehouse.Server.Domain;

namespace ProjectWarehouse.Server.Models.Integrations;

public class SaveAutoMapRuleRequest
{
    [Required]
    public MarketplaceCardField Field { get; init; }

    [Required]
    public MarketplaceRuleOperator Operator { get; init; }

    [Required]
    [MaxLength(500)]
    public string Value { get; init; } = null!;

    [Required]
    public Guid CatalogItemId { get; init; }

    public bool IsEnabled { get; init; } = true;

    public int Priority { get; init; }
}
