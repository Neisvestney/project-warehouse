using System.ComponentModel.DataAnnotations;

namespace ProjectWarehouse.Server.Models.Receipts;

public class CreateReceiptTagRequest
{
    [Required, MaxLength(100)]
    public string Name { get; init; } = null!;
}
