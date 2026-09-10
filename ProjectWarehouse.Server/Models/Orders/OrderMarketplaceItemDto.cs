using ProjectWarehouse.Server.Infrastructure;
using ProjectWarehouse.Server.Models.Integrations;

namespace ProjectWarehouse.Server.Models.Orders;

public class OrderMarketplaceItemDto: IHasIdentity
{
    public Guid Id { get; set; }
    public MarketplaceCardDto? MarketplaceCard { get; set; }
    public Guid? CatalogItemId { get; set; }
    public int Quantity { get; set; }

    public decimal? CustomerPrice { get; set; }
    public string? CustomerCurrencyCode { get; set; }
    public decimal? Price { get; set; }
    public decimal? OldPrice { get; set; }
    public decimal? DiscountValue { get; set; }
    public decimal? Payout { get; set; }
    public string? CurrencyCode { get; set; }
    public decimal? CommissionAmount { get; set; }
    public string? CommissionCurrencyCode { get; set; }
}
