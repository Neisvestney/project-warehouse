using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ProjectWarehouse.Server.Models.Receipts;

public class BatchStandardPlacementRequest
{
    [JsonRequired]
    public Guid StoragePlaceNodeId { get; init; }

    [JsonRequired]
    [MinLength(1)]
    public IReadOnlyList<BatchStandardPlacementItemRequest> Items { get; init; } = [];
}
