using EntityFrameworkCore.Projectables;

namespace ProjectWarehouse.Server.Domain;

public class CatalogItem
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string Article { get; set; } = null!;
    public string? Barcode { get; set; }
    
    public ICollection<CatalogItemWithCharacteristic> Characteristics { get; set; } = []; 
    
    [Projectable]
    public string SearchString =>
        (Name ?? "") + " " + (Article ?? "");
}