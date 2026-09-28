using System.Text.Json.Serialization;

namespace ProjectWarehouse.Server.Models.Tags;

/// <summary>Body of a module's batch tags endpoint; adds or removes one tag on every listed document.</summary>
public class BatchUpdateTagsRequest
{
    public IReadOnlyList<Guid> Ids { get; init; } = [];
    [JsonRequired] public Guid TagId { get; init; }
    [JsonRequired] public TagBatchOperation Operation { get; init; }
}
