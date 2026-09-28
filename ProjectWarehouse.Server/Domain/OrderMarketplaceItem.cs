using EntityFrameworkCore.Projectables;
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

    // ── Financials, as the marketplace last reported them ─────────────────────
    // Ozon does not state whether these are per unit or per line; they are stored verbatim, and Quantity
    // sits next to them so either reading stays recoverable. Refreshed by every order sync until the
    // posting reaches a final status.

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

    /// <summary>Returns matched to this line; a return whose product matched no line is on the order only.</summary>
    public ICollection<MarketplaceReturn> MarketplaceReturns { get; set; } = [];

    /// <summary>Accruals matched to this line by SKU; one about no product of the posting is on the order only.</summary>
    public ICollection<MarketplaceAccrual> MarketplaceAccruals { get; set; } = [];

    [Projectable]
    public int ReturnedQuantity => MarketplaceReturns.Where(r => r.IsCountedAsReturn).Sum(r => r.Quantity);

    [Projectable]
    public int KeptQuantity => Quantity - ReturnedQuantity;

    [Projectable]
    public int ReturnedAfterDeliveryQuantity => MarketplaceReturns
        .Where(r => !r.IsCancelled && r.Kind == MarketplaceReturnKind.CustomerReturn)
        .Sum(r => r.Quantity);

    /// <summary>
    /// <see cref="Payout"/> of the units the buyer kept. The marketplace reverses the sale of a returned unit but
    /// never refreshes <see cref="Payout"/> after delivery, so the reversal is applied here. A partial refusal
    /// happens before the posting is delivered, and its <see cref="Payout"/> already leaves the refused units out.
    /// </summary>
    [Projectable]
    public decimal KeptPayout =>
        Payout > 0 && KeptQuantity > 0
            ? Math.Round(Payout.Value * KeptQuantity / (KeptQuantity + ReturnedAfterDeliveryQuantity), 2)
            : 0;
}
