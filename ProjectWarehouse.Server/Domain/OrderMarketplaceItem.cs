using ProjectWarehouse.Server.Infrastructure;

namespace ProjectWarehouse.Server.Domain;

public class OrderMarketplaceItem : IHasIdentity
{
    public Guid Id { get; set; }

    public Guid OrderId { get; set; }
    public Order Order { get; set; } = null!;

    public string MarketplaceCardId { get; set; } = null!;
    public int Quantity { get; set; }
}
