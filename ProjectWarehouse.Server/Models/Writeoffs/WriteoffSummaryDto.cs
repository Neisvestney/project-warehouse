using ProjectWarehouse.Server.Domain;

namespace ProjectWarehouse.Server.Models.Writeoffs;

public class WriteoffSummaryDto
{
    public Guid Id { get; init; }
    public int Number { get; init; }
    public string? Name { get; init; }
    public WriteoffReason Reason { get; init; }
    public WriteoffStatus Status { get; init; }
    public Guid WarehouseId { get; init; }
    public string WarehouseName { get; init; } = null!;
    public int ItemsCount { get; init; }
    public DateTime CreatedAt { get; init; }
}
