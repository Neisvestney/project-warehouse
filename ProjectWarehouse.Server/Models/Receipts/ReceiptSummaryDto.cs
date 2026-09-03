using ProjectWarehouse.Server.Domain;

namespace ProjectWarehouse.Server.Models.Receipts;

public class ReceiptSummaryDto
{
    public Guid Id { get; init; }
    public int Number { get; init; }
    public string? Name { get; init; }
    public ReceiptReason Reason { get; init; }
    public ReceiptStatus Status { get; init; }
    public Guid WarehouseId { get; init; }
    public string WarehouseName { get; init; } = null!;
    public int ItemsCount { get; init; }
    public int TotalPlannedCount { get; init; }
    public int TotalReceivedCount { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateOnly? PlannedDeliveryDate { get; init; }
    public IReadOnlyList<ReceiptTagDto> Tags { get; init; } = [];
}
