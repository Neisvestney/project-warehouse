using System.ComponentModel.DataAnnotations;

namespace ProjectWarehouse.Server.Models.Orders;

public class CreateOrderTagRequest
{
    [Required, MaxLength(100)]
    public string Name { get; init; } = null!;
}
