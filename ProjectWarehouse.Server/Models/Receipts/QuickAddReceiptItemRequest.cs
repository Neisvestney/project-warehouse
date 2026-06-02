using System.Text.Json.Serialization;

namespace ProjectWarehouse.Server.Models.Receipts;

public class QuickAddReceiptItemRequest
{
    [JsonRequired]
    public Guid CatalogItemId { get; init; }
}
