using ProjectWarehouse.Server.Infrastructure;

namespace ProjectWarehouse.Server.Domain;

/// <summary>
/// A storage node included in a stocktake. Its presence means "this cell was counted" — even when it
/// holds no items, which is how an empty cell is distinguished from one that was never selected.
/// </summary>
public class StocktakeNode : IHasIdentity
{
    public Guid Id { get; set; }

    public Guid StocktakeId { get; set; }
    public Stocktake Stocktake { get; set; } = null!;

    public Guid StoragePlaceNodeId { get; set; }
    public StoragePlaceNode StoragePlaceNode { get; set; } = null!;

    public ICollection<StocktakeItem> Items { get; set; } = [];
}
