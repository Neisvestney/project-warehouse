using ProjectWarehouse.Server.Infrastructure;

namespace ProjectWarehouse.Server.Domain;

public class OrderMarketplaceItem : IHasIdentity
{
    public Guid Id { get; set; }

    public Guid OrderId { get; set; }
    public Order Order { get; set; } = null!;

    public Guid? MarketplaceCardId { get; set; }
    public MarketplaceCard? MarketplaceCard { get; set; }

    public int Quantity { get; set; }
}
