using ProjectWarehouse.Server.Domain;

namespace ProjectWarehouse.Server.Models.Stocktakes;

public class StocktakeSummaryDto
{
    public Guid Id { get; init; }
    public int Number { get; init; }
    public string? Name { get; init; }
    public StocktakeStatus Status { get; init; }
    public Guid WarehouseId { get; init; }
    public string WarehouseName { get; init; } = null!;
    public int NodesCount { get; init; }
    public int ItemsCount { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? StartedAt { get; init; }
    public DateTime? FinishedAt { get; init; }
}
