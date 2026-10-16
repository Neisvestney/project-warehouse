using ProjectWarehouse.Server.Domain;

namespace ProjectWarehouse.Server.Models.Orders;

public class AssemblyTaskDto
{
    public Guid Id { get; init; }
    public AssemblyTaskStatus Status { get; init; }
    public Guid? AssignedToId { get; init; }
    public string? AssignedToName { get; init; }
    public IReadOnlyList<AssemblyTaskBoxDto> Boxes { get; init; } = [];
}
