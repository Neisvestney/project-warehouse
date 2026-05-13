using System.ComponentModel.DataAnnotations;

namespace ProjectWarehouse.Server.Models.Warehouses;

public class UpdateWarehouseRequest
{
    [Required, MinLength(1)]
    public string Name { get; init; } = null!;

    public int Width { get; init; }
    public int Height { get; init; }

    public IReadOnlyList<StoragePlaceItem> StoragePlaces { get; init; } = [];
}