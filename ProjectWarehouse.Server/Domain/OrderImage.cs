using ProjectWarehouse.Server.Infrastructure.Files;

namespace ProjectWarehouse.Server.Domain;

public class OrderImage : IDataFileLink
{
    public Guid Id { get; set; }

    public Guid OrderId { get; set; }

    // Named `OwnerOrder`, not `Order`: the sort-key property required by IDataFileLink is itself
    // named `Order`, and a member can't share that name with the navigation to the `Order` entity.
    public Order OwnerOrder { get; set; } = null!;

    public Guid DataFileId { get; set; }
    public DataFile DataFile { get; set; } = null!;

    public int Order { get; set; }
}
