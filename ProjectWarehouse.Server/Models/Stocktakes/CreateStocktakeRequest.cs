using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ProjectWarehouse.Server.Models.Stocktakes;

public class CreateStocktakeRequest
{
    [StringLength(256)]
    public string? Name { get; init; }

    [JsonRequired]
    public Guid WarehouseId { get; init; }

    [StringLength(2048)]
    public string? Notes { get; init; }
}
