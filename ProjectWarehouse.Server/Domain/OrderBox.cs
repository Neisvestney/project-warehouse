using ProjectWarehouse.Server.Infrastructure;

namespace ProjectWarehouse.Server.Domain;

public class OrderBox : IHasIdentity
{
    public Guid Id { get; set; }

    public Guid OrderId { get; set; }
    public Order Order { get; set; } = null!;

    public string? Label { get; set; }

    public ICollection<OrderBoxComponent> Components { get; set; } = [];
}
