using System.Text.RegularExpressions;
using ProjectWarehouse.Server.Domain;

namespace ProjectWarehouse.Server.Infrastructure.Marketplaces;

/// <summary>A rule with its regex built once, so a run does not recompile the pattern per card.</summary>
public sealed class CompiledAutoMapRule
{
    public required Guid RuleId { get; init; }
    public required Guid CatalogItemId { get; init; }
    public required MarketplaceCardField Field { get; init; }
    public required MarketplaceRuleOperator Operator { get; init; }
    public required string Value { get; init; }
    public Regex? Regex { get; init; }
}

public static class MarketplaceRuleMatcher
{
    /// <summary>A single hostile pattern must not stall the sync of a whole account.</summary>
    public static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(200);

    public static Regex BuildRegex(string pattern) =>
        new(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, RegexTimeout);

    public static CompiledAutoMapRule Compile(MarketplaceAutoMapRule rule) => new()
    {
        RuleId = rule.Id,
        CatalogItemId = rule.CatalogItemId,
        Field = rule.Field,
        Operator = rule.Operator,
        Value = rule.Value,
        Regex = rule.Operator == MarketplaceRuleOperator.Regex ? BuildRegex(rule.Value) : null,
    };

    public static IEnumerable<string> ExtractValues(MarketplaceCard card, MarketplaceCardField field) => field switch
    {
        MarketplaceCardField.OfferId => [card.OfferId],
        MarketplaceCardField.Sku => card.Sku is null ? [] : [card.Sku],
        MarketplaceCardField.ExternalId => [card.ExternalId],
        MarketplaceCardField.Name => [card.Name],
        MarketplaceCardField.Barcode => card.Barcodes,
        _ => [],
    };

    /// <summary>A regex timeout counts as "did not match" — the caller logs it and moves on.</summary>
    public static bool Matches(MarketplaceCard card, CompiledAutoMapRule rule, out bool timedOut)
    {
        timedOut = false;

        foreach (var value in ExtractValues(card, rule.Field))
        {
            if (string.IsNullOrEmpty(value))
                continue;

            try
            {
                if (MatchesValue(value, rule))
                    return true;
            }
            catch (RegexMatchTimeoutException)
            {
                timedOut = true;
                return false;
            }
        }

        return false;
    }

    private static bool MatchesValue(string value, CompiledAutoMapRule rule) => rule.Operator switch
    {
        MarketplaceRuleOperator.Equals => value.Equals(rule.Value, StringComparison.OrdinalIgnoreCase),
        MarketplaceRuleOperator.Contains => value.Contains(rule.Value, StringComparison.OrdinalIgnoreCase),
        MarketplaceRuleOperator.StartsWith => value.StartsWith(rule.Value, StringComparison.OrdinalIgnoreCase),
        MarketplaceRuleOperator.EndsWith => value.EndsWith(rule.Value, StringComparison.OrdinalIgnoreCase),
        MarketplaceRuleOperator.Regex => rule.Regex!.IsMatch(value),
        _ => false,
    };
}
