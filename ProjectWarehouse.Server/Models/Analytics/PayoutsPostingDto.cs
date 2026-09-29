using ProjectWarehouse.Server.Domain;

namespace ProjectWarehouse.Server.Models.Analytics;

public class PayoutsPostingDto
{
    public Guid OrderId { get; init; }
    public int OrderNumber { get; init; }
    public string PostingNumber { get; init; } = null!;
    public Guid MarketplaceAccountId { get; init; }
    public MarketplaceType MarketplaceType { get; init; }
    public string AccountName { get; init; } = null!;
    public MarketplaceOrderStatus Status { get; init; }

    /// <summary>The order's effective date, which the age counts from.</summary>
    public DateTime EffectiveDate { get; init; }

    public DateTime? DeliveredAt { get; init; }
    public int AgeDays { get; init; }

    /// <summary>Σ price × quantity of the posting's lines in the requested currency; not the payout estimate.</summary>
    public decimal Amount { get; init; }
}
