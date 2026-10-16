using System.Text.Json.Serialization;

namespace ProjectWarehouse.Server.Models.Writeoffs;

public class BatchWriteoffTransitionRequest
{
    public IReadOnlyList<Guid> Ids { get; init; } = [];
    [JsonRequired] public WriteoffTransition Transition { get; init; }
}
