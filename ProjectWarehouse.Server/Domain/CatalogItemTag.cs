namespace ProjectWarehouse.Server.Domain;

public class CatalogItemTag : Tag
{
    public ICollection<CatalogItem> Items { get; set; } = [];
}
