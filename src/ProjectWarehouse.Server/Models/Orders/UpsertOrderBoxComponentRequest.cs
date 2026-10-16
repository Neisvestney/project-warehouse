using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ProjectWarehouse.Server.Models.Orders;

public class UpsertOrderBoxComponentRequest
{
    [JsonRequired]
    public Guid CatalogItemId { get; init; }

    [Range(1, int.MaxValue)]
    public int Quantity { get; init; }
}
