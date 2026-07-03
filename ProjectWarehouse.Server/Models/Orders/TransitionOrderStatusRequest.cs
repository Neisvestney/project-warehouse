using System.Text.Json.Serialization;
using ProjectWarehouse.Server.Domain;

namespace ProjectWarehouse.Server.Models.Orders;

public class TransitionOrderStatusRequest
{
    [JsonRequired]
    public OrderStatus TargetStatus { get; init; }
}
