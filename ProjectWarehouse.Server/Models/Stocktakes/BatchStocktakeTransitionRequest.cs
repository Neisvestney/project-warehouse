using System.Text.Json.Serialization;

namespace ProjectWarehouse.Server.Models.Stocktakes;

public class BatchStocktakeTransitionRequest
{
    public IReadOnlyList<Guid> Ids { get; init; } = [];
    [JsonRequired] public StocktakeTransition Transition { get; init; }
}
