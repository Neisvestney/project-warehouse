using System.ComponentModel.DataAnnotations;

namespace ProjectWarehouse.Server.Models.Orders;

/// <summary>Exactly one of TargetBoxId or NewBoxLabel must be set.</summary>
public class MoveOrderBoxComponentRequest
{
    /// <summary>Existing target box. Mutually exclusive with NewBoxLabel.</summary>
    public Guid? TargetBoxId { get; init; }

    /// <summary>Label for a new box to create on the fly. Mutually exclusive with TargetBoxId.</summary>
    [StringLength(256)]
    public string? NewBoxLabel { get; init; }

    [Range(1, int.MaxValue)]
    public int Quantity { get; init; }
}
