using ProjectWarehouse.Server.Infrastructure;

namespace ProjectWarehouse.Server.Domain;

public class AssemblyTaskBox : IHasIdentity
{
    public Guid Id { get; set; }

    public Guid AssemblyTaskId { get; set; }
    public AssemblyTask AssemblyTask { get; set; } = null!;

    public Guid OrderBoxId { get; set; }
    public OrderBox OrderBox { get; set; } = null!;

    public ICollection<AssemblyTaskBoxComponent> Components { get; set; } = [];
}
