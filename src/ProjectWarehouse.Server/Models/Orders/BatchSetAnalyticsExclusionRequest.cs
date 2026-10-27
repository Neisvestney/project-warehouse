using System.Text.Json.Serialization;

namespace ProjectWarehouse.Server.Models.Orders;

public class BatchSetAnalyticsExclusionRequest
{
    public IReadOnlyList<Guid> OrderIds { get; init; } = [];

    [JsonRequired]
    public bool Excluded { get; init; }
}
