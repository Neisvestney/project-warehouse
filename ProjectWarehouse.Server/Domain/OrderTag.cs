namespace ProjectWarehouse.Server.Domain;

public class OrderTag : Tag
{
    public ICollection<Order> Orders { get; set; } = [];
}
