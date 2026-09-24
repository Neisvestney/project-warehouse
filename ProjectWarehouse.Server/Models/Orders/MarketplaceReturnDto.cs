using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Infrastructure;

namespace ProjectWarehouse.Server.Models.Orders;

/// <summary>One returned unit of the order, as the marketplace reported it.</summary>
public class MarketplaceReturnDto : IHasIdentity
{
    public Guid Id { get; init; }
    public string ExternalId { get; init; } = null!;

    /// <summary>The order line it was matched to; null when its product matched none.</summary>
    public Guid? OrderMarketplaceItemId { get; init; }
    public Guid? CatalogItemId { get; init; }
    public string? Sku { get; init; }
    public string OfferId { get; init; } = null!;

    public MarketplaceReturnScheme Scheme { get; init; }
    public MarketplaceReturnKind Kind { get; init; }
    public string? RawKind { get; init; }
    public int Quantity { get; init; }
    public decimal? Price { get; init; }
    public string? CurrencyCode { get; init; }
    public string? Reason { get; init; }

    public string? RawStatus { get; init; }
    public string? StatusName { get; init; }
    public DateTime? StatusChangedAt { get; init; }
    public bool IsCancelled { get; init; }

    /// <summary>Whether it counts toward the order's returned units and <see cref="MarketplaceOrderDto.ReturnState"/>.</summary>
    public bool IsCountedAsReturn { get; init; }

    public DateTime? ReturnedAt { get; init; }
    public DateTime? FinalAt { get; init; }

    public MarketplaceReturnCompensationStatus? CompensationStatus { get; init; }
    public DateTime? CompensationStatusChangedAt { get; init; }
}
