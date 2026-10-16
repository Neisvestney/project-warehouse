namespace ProjectWarehouse.Server.Domain;

public class CatalogItemVariationMember
{
    public Guid ItemId { get; set; }
    public CatalogItem Item { get; set; } = null!;

    public Guid VariationId { get; set; }
    public CatalogItem Variation { get; set; } = null!;
}
