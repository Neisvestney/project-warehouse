using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using ProjectWarehouse.Server.Domain;

namespace ProjectWarehouse.Server.Models.Receipts;

public class UpdateReceiptRequest
{
    [Required]
    [MaxLength(256)]
    public string Name { get; init; } = null!;

    [JsonRequired]
    public ReceiptReason Reason { get; init; }

    [MaxLength(2048)]
    public string? Notes { get; init; }
}
