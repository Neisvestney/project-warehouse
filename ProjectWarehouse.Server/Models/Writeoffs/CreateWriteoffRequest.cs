using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using ProjectWarehouse.Server.Domain;

namespace ProjectWarehouse.Server.Models.Writeoffs;

public class CreateWriteoffRequest
{
    [StringLength(256)]
    public string? Name { get; init; }

    [JsonRequired]
    public WriteoffReason Reason { get; init; }

    [JsonRequired]
    public Guid WarehouseId { get; init; }

    [StringLength(2048)]
    public string? Notes { get; init; }
}
