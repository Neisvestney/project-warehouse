namespace ProjectWarehouse.Server.Models.Stocktakes;

public class StocktakeNodeDto
{
    public Guid Id { get; init; }
    public Guid StoragePlaceNodeId { get; init; }
    public string[] NodePath { get; init; } = [];
    public IReadOnlyList<StocktakeItemDto> Items { get; init; } = [];
}
