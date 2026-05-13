using System.ComponentModel.DataAnnotations;

namespace ProjectWarehouse.Server.Models.Warehouses;

public class StoragePlaceItem
{
    public Guid? Id { get; init; }

    [Required, MinLength(1)]
    public string Name { get; init; } = null!;

    public int X { get; init; }
    public int Y { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
}