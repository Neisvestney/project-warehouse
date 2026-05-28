using System.Text.Json.Serialization;

namespace ProjectWarehouse.Server.Models.Receipts;

public class CreateUnitPlacementRequest
{
    [JsonRequired]
    public Guid StoragePlaceNodeId { get; init; }

    [JsonRequired]
    public CreateUnitItemRequest UnitItem { get; init; } = null!;
}
