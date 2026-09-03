using EntityFrameworkCore.Projectables;
using ProjectWarehouse.Server.Infrastructure;

namespace ProjectWarehouse.Server.Domain;

public abstract class Tag : IHasIdentity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;

    [Projectable] public string SearchString => Name;
}
