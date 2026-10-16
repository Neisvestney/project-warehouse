namespace ProjectWarehouse.Server.Integrations.Abstractions;

/// <summary>
/// A posting item carries no card id: Ozon's postings expose <c>sku</c> and <c>offer_id</c>, never
/// <c>product_id</c> — and <see cref="Domain.MarketplaceCard.ExternalId"/> is exactly product_id.
/// The sync service resolves the card by <see cref="Sku"/> first, then <see cref="OfferId"/>.
/// </summary>
/// <remarks>
/// Every money field is optional: a marketplace may not report financials at all, and Ozon fills them
/// only for postings whose financial block it has already computed. Amounts are carried exactly as the
/// marketplace states them — see <see cref="Domain.OrderMarketplaceItem"/> for the per-unit caveat.
/// </remarks>
public record ExternalPostingItem(
    string? Sku,
    string OfferId,
    string Name,
    int Quantity,
    decimal? CustomerPrice = null,
    string? CustomerCurrencyCode = null,
    decimal? Price = null,
    decimal? OldPrice = null,
    decimal? DiscountValue = null,
    decimal? Payout = null,
    string? CurrencyCode = null,
    decimal? CommissionAmount = null,
    string? CommissionCurrencyCode = null);
