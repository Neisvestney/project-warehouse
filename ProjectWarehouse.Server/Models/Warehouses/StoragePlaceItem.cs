using System.ComponentModel.DataAnnotations;

namespace ProjectWarehouse.Server.Models.Warehouses;

public class StoragePlaceItem
{
    public Guid? Id { get; init; }

    [Required, MinLength(1)]
    public string Name { get; init; } = null!;

    public decimal X { get; init; }
    public decimal Y { get; init; }
    public decimal Width { get; init; }
    public decimal Height { get; init; }
}