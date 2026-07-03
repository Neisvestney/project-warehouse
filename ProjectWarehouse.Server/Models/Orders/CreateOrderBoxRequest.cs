using System.ComponentModel.DataAnnotations;

namespace ProjectWarehouse.Server.Models.Orders;

public class CreateOrderBoxRequest
{
    [StringLength(256)]
    public string? Label { get; init; }
}
