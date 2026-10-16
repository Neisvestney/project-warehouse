using ProjectWarehouse.Server.Infrastructure;

namespace ProjectWarehouse.Server.Domain;

public class AssemblyTask : IHasIdentity
{
    public Guid Id { get; set; }

    public Guid OrderId { get; set; }
    public Order Order { get; set; } = null!;

    public Guid? AssignedToId { get; set; }
    public ApplicationUser? AssignedTo { get; set; }

    public AssemblyTaskStatus Status { get; set; } = AssemblyTaskStatus.Pending;

    public ICollection<AssemblyTaskBox> Boxes { get; set; } = [];
}
