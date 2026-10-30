using ProjectWarehouse.Server.Domain;

namespace ProjectWarehouse.Server.Models.Orders;

public class OrderFinanceDto
{
    public IReadOnlyList<OrderFinanceAccrualDto> Accruals { get; init; } = [];
    public IReadOnlyList<OrderExemplarHistoryDto> Exemplars { get; init; } = [];
}

public class OrderFinanceAccrualDto
{
    public Guid Id { get; init; }
    public string ExternalId { get; init; } = null!;
    public DateOnly Date { get; init; }
    public DateTime? OccurredAt { get; init; }
    public MarketplaceAccrualCategory Category { get; init; }
    public MarketplaceAccrualSource Source { get; init; }
    public string? RawTypeId { get; init; }
    public string? UnitNumber { get; init; }
    public string? Rid { get; init; }
    public string? ExemplarId { get; init; }
    public string? Sku { get; init; }
    public decimal Amount { get; init; }
    public string? CurrencyCode { get; init; }
    public Guid? OrderId { get; init; }
    public int? OrderNumber { get; init; }
    public string? PostingNumber { get; init; }
    public string? ItemName { get; init; }
}

public class OrderExemplarHistoryDto
{
    public string ExemplarId { get; init; } = null!;
    public bool HasRestrictedHistory { get; init; }
    public IReadOnlyList<OrderFinanceAccrualDto> Accruals { get; init; } = [];
    public IReadOnlyList<OrderExemplarOrderDto> Orders { get; init; } = [];
    public IReadOnlyList<OrderExemplarReturnDto> Returns { get; init; } = [];
}

public class OrderExemplarOrderDto
{
    public Guid Id { get; init; }
    public int Number { get; init; }
    public string PostingNumber { get; init; } = null!;
    public OrderType Type { get; init; }
    public MarketplaceOrderStatus Status { get; init; }
    public DateTime? InProcessAt { get; init; }
    public DateTime? DeliveringAt { get; init; }
    public DateTime? DeliveredAt { get; init; }
    public DateTime? CancelledAt { get; init; }
    public bool IsReconstructed { get; init; }
}

public class OrderExemplarReturnDto
{
    public Guid Id { get; init; }
    public Guid? OrderId { get; init; }
    public MarketplaceReturnKind Kind { get; init; }
    public DateOnly? RequestedOn { get; init; }
    public DateTime? ReturnedAt { get; init; }
    public DateTime? FinalAt { get; init; }
    public DateOnly? SaleReversedOn { get; init; }
    public string? StatusName { get; init; }
    public string? Reason { get; init; }
    public bool IsCancelled { get; init; }
}
