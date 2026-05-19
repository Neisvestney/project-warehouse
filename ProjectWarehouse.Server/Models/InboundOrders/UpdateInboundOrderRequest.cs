using System.ComponentModel.DataAnnotations;

namespace ProjectWarehouse.Server.Models.InboundOrders;

public class UpdateInboundOrderRequest
{
    [Required] public Guid WarehouseId { get; init; }
    public string? Title { get; init; }
    [Required] public DateTime PlannedStartDateTime { get; init; }
    public string? Notes { get; init; }
    public IReadOnlyList<Guid> AssignedUserIds { get; init; } = [];
}
