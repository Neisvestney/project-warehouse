using System.ComponentModel.DataAnnotations;

namespace ProjectWarehouse.Server.Models.InboundOrderProcessing;

public class NodeItemEntry
{
    [Required] public Guid CatalogItemWithCharacteristicId { get; init; }
    [Range(1, int.MaxValue)] public int Count { get; init; }
}

public class PlaceItemsRequest
{
    [Required] public IReadOnlyList<NodeItemEntry> Items { get; init; } = [];
}
