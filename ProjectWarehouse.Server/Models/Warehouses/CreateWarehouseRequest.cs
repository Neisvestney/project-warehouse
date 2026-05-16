using System.ComponentModel.DataAnnotations;

namespace ProjectWarehouse.Server.Models.Warehouses;

public class CreateWarehouseRequest
{
    [Required, MinLength(1)]
    public string Name { get; init; } = null!;

    public decimal Width { get; init; }
    public decimal Height { get; init; }

    public IReadOnlyList<StoragePlaceItem> StoragePlaces { get; init; } = [];
}