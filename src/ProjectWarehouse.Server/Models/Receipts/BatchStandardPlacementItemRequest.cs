using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ProjectWarehouse.Server.Models.Receipts;

public class BatchStandardPlacementItemRequest
{
    [JsonRequired]
    public Guid ItemId { get; init; }

    [Range(1, int.MaxValue)]
    public int Count { get; init; }
}
