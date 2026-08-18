using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using ProjectWarehouse.Server.Domain;

namespace ProjectWarehouse.Server.Models.Stocktakes;

public class CreateStocktakeRequest
{
    [StringLength(256)]
    public string? Name { get; init; }

    [JsonRequired]
    public Guid WarehouseId { get; init; }

    public StocktakeType Type { get; init; } = StocktakeType.Unscheduled;

    /// <summary>Required when the type is Scheduled, ignored otherwise.</summary>
    public DateOnly? PlannedDate { get; init; }

    [StringLength(2048)]
    public string? Notes { get; init; }
}
