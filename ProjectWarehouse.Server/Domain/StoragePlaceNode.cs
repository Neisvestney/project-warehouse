namespace ProjectWarehouse.Server.Domain;

public class StoragePlaceNode
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    
    public Guid RootStoragePlaceId { get; set; }
    public StoragePlace RootStoragePlace { get; set; } = null!;
    
    public Guid? ParentNodeId { get; set; }
    public StoragePlaceNode? ParentNode { get; set; }
    public ICollection<StoragePlaceNode> ChildrenNodes { get; set; } = [];
}