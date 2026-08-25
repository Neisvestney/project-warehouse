using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using ProjectWarehouse.Server.Infrastructure;

namespace ProjectWarehouse.Server.Models.Forecast;

public class SetStockWarningOverrideRequest
{
    [JsonRequired] public Guid WarehouseId { get; init; }

    [JsonRequired] public Guid CatalogItemId { get; init; }

    /// <summary>
    /// Null deletes the override. Writing the warehouse's current value instead would freeze the item
    /// at that number and silently cut it off from later changes to the warehouse setting.
    /// </summary>
    [Range(0, StockForecastCalculator.MaxWarningDays)]
    public int? WarningDays { get; init; }
}
