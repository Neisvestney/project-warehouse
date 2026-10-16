using System.Text.Json.Serialization;

namespace ProjectWarehouse.Server.Models.Orders;

public class BatchAssignRequest
{
    public IReadOnlyList<Guid> OrderIds { get; init; } = [];

    [JsonRequired]
    public Guid AssigneeId { get; init; }
}
