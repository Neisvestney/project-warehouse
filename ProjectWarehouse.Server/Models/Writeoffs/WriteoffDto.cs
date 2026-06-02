using ProjectWarehouse.Server.Domain;

namespace ProjectWarehouse.Server.Models.Writeoffs;

public class WriteoffDto
{
    public Guid Id { get; init; }
    public int Number { get; init; }
    public string Name { get; init; } = null!;
    public WriteoffReason Reason { get; init; }
    public WriteoffStatus Status { get; init; }
    public string? Notes { get; init; }
    public DateTime CreatedAt { get; init; }
    public Guid WarehouseId { get; init; }
    public string WarehouseName { get; init; } = null!;
    public IReadOnlyList<WriteoffItemDto> Items { get; init; } = [];
}
