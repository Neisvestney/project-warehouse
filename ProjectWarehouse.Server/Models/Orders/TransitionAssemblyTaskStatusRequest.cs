using System.Text.Json.Serialization;
using ProjectWarehouse.Server.Domain;

namespace ProjectWarehouse.Server.Models.Orders;

public class TransitionAssemblyTaskStatusRequest
{
    [JsonRequired]
    public AssemblyTaskStatus TargetStatus { get; init; }
}
