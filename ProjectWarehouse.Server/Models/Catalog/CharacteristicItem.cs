using System.ComponentModel.DataAnnotations;
using ProjectWarehouse.Server.Infrastructure;

namespace ProjectWarehouse.Server.Models.Catalog;

public class CharacteristicItem : IHasNullableIdentity
{
    public Guid? Id { get; init; }

    [Required, MinLength(1)]
    public string Characteristic { get; init; } = null!;

    public string? Barcode { get; init; }
}