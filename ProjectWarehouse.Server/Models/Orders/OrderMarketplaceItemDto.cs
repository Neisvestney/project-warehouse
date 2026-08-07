using ProjectWarehouse.Server.Infrastructure;
using ProjectWarehouse.Server.Models.Integrations;

namespace ProjectWarehouse.Server.Models.Orders;

public class OrderMarketplaceItemDto: IHasIdentity
{
    public Guid Id { get; set; }
    public MarketplaceCardDto? MarketplaceCard { get; set; }
    public int Quantity { get; set; }
}