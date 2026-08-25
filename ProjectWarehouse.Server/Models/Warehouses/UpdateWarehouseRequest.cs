using System.ComponentModel.DataAnnotations;

namespace ProjectWarehouse.Server.Models.Warehouses;

public class UpdateWarehouseRequest
{
    [Required, MinLength(1)]
    public string Name { get; init; } = null!;

    public decimal Width { get; init; }
    public decimal Height { get; init; }

    public Guid? DefaultStoragePlaceNodeId { get; init; }

    /// <summary>IANA identifier; null falls back to the caller's zone and then to the server's.</summary>
    public string? TimeZoneId { get; init; }

    public IReadOnlyList<StoragePlaceItem> StoragePlaces { get; init; } = [];
    public IReadOnlyList<WarehouseLayoutElementItem> LayoutObjects { get; init; } = [];
}