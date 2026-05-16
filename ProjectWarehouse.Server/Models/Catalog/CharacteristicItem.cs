using System.ComponentModel.DataAnnotations;

namespace ProjectWarehouse.Server.Models.Catalog;

public class CharacteristicItem
{
    public Guid? Id { get; init; }

    [Required, MinLength(1)]
    public string Characteristic { get; init; } = null!;

    public string? Barcode { get; init; }
}