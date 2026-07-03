using ProjectWarehouse.Server.Domain;

namespace ProjectWarehouse.Server.Models.Orders;

public class OrderSummaryDto
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
    public int BoxCount { get; init; }
    public int ComponentCount { get; init; }
}
