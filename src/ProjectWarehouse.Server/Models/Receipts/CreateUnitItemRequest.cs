using System.ComponentModel.DataAnnotations;

namespace ProjectWarehouse.Server.Models.Receipts;

/// <summary>Data required to create a new unit (serialised) inventory item.</summary>
public class CreateUnitItemRequest
{
    [Required]
    [MaxLength(256)]
    public string InventoryNumber { get; init; } = null!;
}
