namespace ProjectWarehouse.Server.Infrastructure.Marketplaces;

/// <summary>
/// A card the order import invented because a posting named a product the card list never mentioned.
/// </summary>
/// <remarks>
/// <see cref="Domain.MarketplaceCard.ExternalId"/> is the marketplace's product id, and a posting never
/// carries one — so a placeholder is keyed on what the posting does carry, behind a prefix no real
/// product id can take. That keeps the unique index honest and lets the card sync recognize the row and
/// adopt it once the real card appears.
/// </remarks>
public static class MarketplaceCardPlaceholder
{
    private const string SkuPrefix = "sku:";
    private const string OfferPrefix = "offer:";

    /// <summary>Null when the posting names the product by neither sku nor seller article.</summary>
    public static string? ExternalIdFor(string? sku, string? offerId) => (sku, offerId) switch
    {
        ({ Length: > 0 }, _) => SkuPrefix + sku,
        (_, { Length: > 0 }) => OfferPrefix + offerId,
        _ => null,
    };

    public static bool IsPlaceholder(string externalId) =>
        externalId.StartsWith(SkuPrefix, StringComparison.Ordinal)
        || externalId.StartsWith(OfferPrefix, StringComparison.Ordinal);
}
