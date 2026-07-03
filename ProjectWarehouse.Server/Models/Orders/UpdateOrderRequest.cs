using System.ComponentModel.DataAnnotations;

namespace ProjectWarehouse.Server.Models.Orders;

public class UpdateOrderRequest
{
    [StringLength(2048)]
    public string? Notes { get; init; }

    public DateTime? PlannedShipmentAt { get; init; }
}
