using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using ProjectWarehouse.Server.Domain;

namespace ProjectWarehouse.Server.Models.Receipts;

public class CreateReceiptRequest
{
    [MaxLength(256)]
    public string? Name { get; init; }

    [JsonRequired]
    public ReceiptReason Reason { get; init; }

    [JsonRequired]
    public Guid WarehouseId { get; init; }

    [MaxLength(2048)]
    public string? Notes { get; init; }

    public DateOnly? PlannedDeliveryDate { get; init; }
}
