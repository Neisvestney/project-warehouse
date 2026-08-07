using ProjectWarehouse.Server.Domain;

namespace ProjectWarehouse.Server.Models.Orders;

public class OrderDetailsDto
{
    public Guid Id { get; init; }
    public int Number { get; init; }
    public OrderType Type { get; init; }
    public OrderStatus Status { get; init; }
    public string? Notes { get; init; }
    public DateTime? PlannedShipmentAt { get; init; }
    public DateTime CreatedAt { get; init; }
    public Guid WarehouseId { get; init; }
    public string WarehouseName { get; init; } = null!;
    public string? CreatedByName { get; init; }
    public MarketplaceOrderDto? MarketplaceOrder { get; init; }
    public IReadOnlyList<OrderMarketplaceItemDto> MarketplaceItems { get; init; } = [];
    public IReadOnlyList<OrderBoxDto> Boxes { get; init; } = [];
    public IReadOnlyList<AssemblyTaskDto> AssemblyTasks { get; init; } = [];
}
