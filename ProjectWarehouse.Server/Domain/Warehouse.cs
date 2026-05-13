namespace ProjectWarehouse.Server.Domain;

public class Warehouse
{
    public Guid Id { get; set; }
    public string Name {get; set;} = null!;
    public int Width {get; set;}
    public int Height {get; set;}

    public ICollection<StoragePlace> StoragePlaces { get; set; } = [];
}