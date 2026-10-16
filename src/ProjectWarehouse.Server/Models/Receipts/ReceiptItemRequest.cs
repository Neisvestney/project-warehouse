using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ProjectWarehouse.Server.Models.Receipts;

public class ReceiptItemRequest
{
    [JsonRequired]
    public Guid CatalogItemId { get; init; }

    [Range(1, int.MaxValue)]
    public int PlannedCount { get; init; }

    [MaxLength(2048)]
    public string? Notes { get; init; }
}
