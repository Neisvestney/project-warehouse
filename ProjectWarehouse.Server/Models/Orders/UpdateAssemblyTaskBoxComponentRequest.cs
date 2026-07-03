using System.ComponentModel.DataAnnotations;

namespace ProjectWarehouse.Server.Models.Orders;

public class UpdateAssemblyTaskBoxComponentRequest
{
    [Range(1, int.MaxValue)]
    public int Quantity { get; init; }
}
