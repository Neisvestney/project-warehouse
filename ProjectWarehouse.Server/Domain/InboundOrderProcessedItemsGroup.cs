namespace ProjectWarehouse.Server.Domain;

public class InboundOrderProcessedItemsGroup: ItemsGroup
{
    public Guid InboundOrderId {get; set;}
    public InboundOrder InboundOrder { get; set; } = null!;

    public Guid? StoragePlaceNodeId { get; set; }
    public StoragePlaceNode? StoragePlaceNode { get; set; }
}
