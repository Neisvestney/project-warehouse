using EntityFrameworkCore.Projectables;
using ProjectWarehouse.Server.Infrastructure;

namespace ProjectWarehouse.Server.Domain;

public class Warehouse : IHasIdentity
{
    public Guid Id { get; set; }
    public string Name {get; set;} = null!;
    public decimal Width {get; set;}
    public decimal Height {get; set;}

    /// <summary>
    /// IANA identifier (<c>Europe/Moscow</c>) the warehouse's days are cut by. Null falls back to the
    /// caller's zone and then to the server's. An identifier rather than an offset: a stored offset
    /// drifts away from reality at every DST transition and legislative zone change.
    /// </summary>
    public string? TimeZoneId { get; set; }

    // Nullable so the system default stays a live constant: a warehouse that configured nothing picks up
    // a change to it, and "null" stays distinguishable from "set to the same number".
    /// <summary>Days left at or below which a position is a warning. Null means the system default.</summary>
    public int? StockWarningDays { get; set; }

    /// <summary>Length of the consumption averaging window. Null means the system default.</summary>
    public int? ConsumptionWindowDays { get; set; }

    /// <summary>Weigh fresh days heavier instead of averaging the window flat.</summary>
    public bool UseWeightedConsumption { get; set; }

    public Guid? DefaultStoragePlaceNodeId { get; set; }
    public StoragePlaceNode? DefaultStoragePlaceNode { get; set; }

    public ICollection<StoragePlace> StoragePlaces { get; set; } = [];
    public ICollection<WarehouseLayoutElement> LayoutObjects { get; set; } = [];

    public ICollection<ApplicationUser> AssignedUsers { get; set; } = [];
    public ICollection<MarketplaceWarehouse> MarketplaceWarehouses { get; set; } = [];
    
    [Projectable]
    public string SearchString => Name;

    [Projectable]
    public int TotalItemsCount => StoragePlaces.Sum(p => p.TotalItemsCount);
}
