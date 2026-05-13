using ProjectWarehouse.Server.Infrastructure;

namespace ProjectWarehouse.Server.Domain;

public class StoragePlace : WarehouseLayoutObject
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;

    public Guid WarehouseId { get; set; }
    public Warehouse Warehouse { get; set; } = null!;
    
    public ICollection<StoragePlaceNode> StoragePlaceNodes { get; set; } = [];
}