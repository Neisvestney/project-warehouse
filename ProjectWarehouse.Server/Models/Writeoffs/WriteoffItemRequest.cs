using System.Text.Json.Serialization;

namespace ProjectWarehouse.Server.Models.Writeoffs;

public class WriteoffItemRequest
{
    [JsonRequired]
    public Guid SourceNodeId { get; init; }

    // Standard item: both must be non-null when using this discriminator
    public Guid? CatalogItemId { get; init; }
    public int? Count { get; init; }

    // Unit item
    public Guid? UnitInventoryItemId { get; init; }

    public string? Notes { get; init; }
}
