namespace ProjectWarehouse.Server.Integrations.Abstractions;

/// <summary>
/// A posting item carries no card id: Ozon's postings expose <c>sku</c> and <c>offer_id</c>, never
/// <c>product_id</c> — and <see cref="Domain.MarketplaceCard.ExternalId"/> is exactly product_id.
/// The sync service resolves the card by <see cref="Sku"/> first, then <see cref="OfferId"/>.
/// </summary>
public record ExternalPostingItem(
    string? Sku,
    string OfferId,
    string Name,
    int Quantity);
