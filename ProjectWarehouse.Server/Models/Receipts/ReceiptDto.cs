using ProjectWarehouse.Server.Domain;

namespace ProjectWarehouse.Server.Models.Receipts;

public class ReceiptDto
{
    public Guid Id { get; init; }
    public int Number { get; init; }
    public string Name { get; init; } = null!;
    public ReceiptReason Reason { get; init; }
    public ReceiptStatus Status { get; init; }
    public string? Notes { get; init; }
    public DateOnly? PlannedDeliveryDate { get; init; }
    public DateTime CreatedAt { get; init; }
    public Guid WarehouseId { get; init; }
    public string WarehouseName { get; init; } = null!;
    public int TotalPlannedCount { get; init; }
    public int TotalReceivedCount { get; init; }
    public IReadOnlyList<ReceiptItemDto> Items { get; init; } = [];
}
