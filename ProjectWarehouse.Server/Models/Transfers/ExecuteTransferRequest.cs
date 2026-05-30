using System.ComponentModel.DataAnnotations;

namespace ProjectWarehouse.Server.Models.Transfers;

public record ExecuteTransferRequest
{
    [Required]
    public Guid FromNodeId { get; init; }

    [Required]
    public Guid ToNodeId { get; init; }

    [Required]
    public IReadOnlyList<TransferItemRequest> Items { get; init; } = [];
}

public record TransferItemRequest
{
    /// <summary>Filled for Standard (count-based) items. Requires <see cref="Count"/>.</summary>
    public Guid? CatalogItemId { get; init; }

    /// <summary>Required when <see cref="CatalogItemId"/> is set.</summary>
    public int? Count { get; init; }

    /// <summary>Filled for Unit items.</summary>
    public Guid? UnitItemId { get; init; }

    /// <summary>Filled for AssembledBundle items.</summary>
    public Guid? AssembledBundleItemId { get; init; }
}
