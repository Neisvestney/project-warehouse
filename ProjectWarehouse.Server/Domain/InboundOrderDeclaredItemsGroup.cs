namespace ProjectWarehouse.Server.Domain;

public class InboundOrderDeclaredItemsGroup: ItemsGroup
{
    public Guid InboundOrderId {get; set;}
    public InboundOrder InboundOrder { get; set; } = null!;
}
