using ProjectWarehouse.Server.Domain;

namespace ProjectWarehouse.Server.Models.Orders;

public class MarketplaceSupplyDto
{
    public Guid Id { get; init; }
    public Guid MarketplaceAccountId { get; init; }
    public string MarketplaceAccountName { get; init; } = null!;
    public MarketplaceType MarketplaceType { get; init; }

    public string ExternalId { get; init; } = null!;

    /// <summary>Seller-given name; null when the seller left it empty.</summary>
    public string? Name { get; init; }

    public DateTime CreatedAt { get; init; }

    /// <summary>When the supply was handed over to delivery; null while it is open.</summary>
    public DateTime? ClosedAt { get; init; }

    public bool IsDone { get; init; }
}
