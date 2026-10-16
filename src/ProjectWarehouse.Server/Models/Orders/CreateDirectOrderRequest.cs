using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ProjectWarehouse.Server.Models.Orders;

public class CreateDirectOrderRequest
{
    [JsonRequired]
    public Guid WarehouseId { get; init; }

    [StringLength(2048)]
    public string? Notes { get; init; }

    public DateTime? PlannedShipmentAt { get; init; }
}
