namespace ProjectWarehouse.Server.Models.Orders;

public class AssemblyTaskBoxDto
{
    public Guid Id { get; init; }
    public Guid OrderBoxId { get; init; }
    public string? OrderBoxLabel { get; init; }
    public IReadOnlyList<AssemblyTaskBoxComponentDto> Components { get; init; } = [];
}
