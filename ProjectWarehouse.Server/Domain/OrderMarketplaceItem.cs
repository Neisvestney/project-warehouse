using ProjectWarehouse.Server.Infrastructure;

namespace ProjectWarehouse.Server.Domain;

public class OrderMarketplaceItem : IHasIdentity
{
    public Guid Id { get; set; }

    public Guid OrderId { get; set; }
    public Order Order { get; set; } = null!;

    public Guid? MarketplaceCardId { get; set; }
    public MarketplaceCard? MarketplaceCard { get; set; }

    /// <summary>
    /// The catalog item the card pointed at when the posting was imported. A snapshot on purpose:
    /// <see cref="MarketplaceCard.CatalogItemId"/> is remapped by hand, and reporting through the live
    /// mapping would move last year's revenue onto another item the day someone edits it.
    /// </summary>
    public Guid? CatalogItemId { get; set; }
    public CatalogItem? CatalogItem { get; set; }

    public int Quantity { get; set; }

    // ── Financials, as the marketplace reported them at import ────────────────
    // Ozon does not state whether these are per unit or per line; they are stored verbatim, and Quantity
    // sits next to them so either reading stays recoverable. Never refreshed: a posting reaches WMS in
    // awaiting_deliver, by which point the buyer has already paid.

    /// <summary>What the buyer paid, in the buyer's own currency.</summary>
    public decimal? CustomerPrice { get; set; }
    public string? CustomerCurrencyCode { get; set; }

    /// <summary>Price with promotions applied, excluding those funded by the marketplace.</summary>
    public decimal? Price { get; set; }

    /// <summary>Price before discounts — the struck-through one on the product card.</summary>
    public decimal? OldPrice { get; set; }

    public decimal? DiscountValue { get; set; }

    /// <summary>Seller payout after the marketplace's cut.</summary>
    public decimal? Payout { get; set; }

    /// <summary>Currency of <see cref="Price"/>, <see cref="OldPrice"/>, <see cref="DiscountValue"/> and <see cref="Payout"/>.</summary>
    public string? CurrencyCode { get; set; }

    public decimal? CommissionAmount { get; set; }
    public string? CommissionCurrencyCode { get; set; }
}
