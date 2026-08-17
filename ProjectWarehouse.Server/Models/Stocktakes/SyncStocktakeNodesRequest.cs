using System.Text.Json.Serialization;

namespace ProjectWarehouse.Server.Models.Stocktakes;

public class SyncStocktakeNodesRequest
{
    /// <summary>The full desired scope. Nodes already present keep their counted items.</summary>
    [JsonRequired]
    public IReadOnlyList<Guid> NodeIds { get; init; } = [];
}
