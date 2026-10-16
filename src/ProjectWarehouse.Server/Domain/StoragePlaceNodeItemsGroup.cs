namespace ProjectWarehouse.Server.Domain;

public class StoragePlaceNodeItemsGroup: ItemsGroup
{
    public Guid StoragePlaceNodeId {get; set;}
    public StoragePlaceNode StoragePlaceNode { get; set; } = null!;
}