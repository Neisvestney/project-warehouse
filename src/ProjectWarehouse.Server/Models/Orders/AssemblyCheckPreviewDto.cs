namespace ProjectWarehouse.Server.Models.Orders;

/// <summary>What is still left to assemble in the requested orders, shaped as assembly tasks.</summary>
public class AssemblyCheckPreviewDto
{
    public IReadOnlyList<AssemblyCheckTaskDto> Tasks { get; init; } = [];
}

public class AssemblyCheckTaskDto
{
    public Guid OrderId { get; init; }
    public int OrderNumber { get; init; }
    public string? PostingNumber { get; init; }
    public Guid WarehouseId { get; init; }

    /// <summary>
    /// A real task of an <c>Assembly</c> order, or one built from the order's boxes for <c>Draft</c> and
    /// <c>Confirmed</c>. Component <c>quantity</c> is what is still left to assemble and <c>fulfillments</c> is
    /// always empty.
    /// </summary>
    public required AssemblyTaskDto Task { get; init; }
}
