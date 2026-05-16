using EntityFrameworkCore.Projectables;

namespace ProjectWarehouse.Server.Domain;

public class Warehouse
{
    public Guid Id { get; set; }
    public string Name {get; set;} = null!;
    public decimal Width {get; set;}
    public decimal Height {get; set;}

    public ICollection<StoragePlace> StoragePlaces { get; set; } = [];

    [Projectable]
    public int TotalItemsCount => StoragePlaces.Sum(p => p.TotalItemsCount);
}