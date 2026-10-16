using ProjectWarehouse.Server.Domain;

namespace ProjectWarehouse.Server.Models.Warehouses;

public class WarehouseLayoutElementItem
{
    public decimal X { get; init; }
    public decimal Y { get; init; }
    public decimal Width { get; init; }
    public decimal Height { get; init; }
    public decimal Rotation { get; init; }
    public WarehouseLayoutObjectType Type { get; init; }
}
