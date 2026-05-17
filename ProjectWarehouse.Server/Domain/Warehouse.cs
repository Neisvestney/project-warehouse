using EntityFrameworkCore.Projectables;
using ProjectWarehouse.Server.Infrastructure;

namespace ProjectWarehouse.Server.Domain;

public class Warehouse : IHasIdentity
{
    public Guid Id { get; set; }
    public string Name {get; set;} = null!;
    public decimal Width {get; set;}
    public decimal Height {get; set;}

    public ICollection<StoragePlace> StoragePlaces { get; set; } = [];
    public ICollection<WarehouseLayoutElement> LayoutObjects { get; set; } = [];
    
    public ICollection<ApplicationUser> AssignedUsers { get; set; } = [];

    [Projectable]
    public int TotalItemsCount => StoragePlaces.Sum(p => p.TotalItemsCount);
}
