namespace ProjectWarehouse.Server.Models.Catalog;

public class CatalogItemCharacteristicDto
{
    public Guid Id { get; init; }
    public string Characteristic { get; init; } = null!;
    public string? Barcode { get; init; }
}