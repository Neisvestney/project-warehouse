using ProjectWarehouse.Server.Domain;

namespace ProjectWarehouse.Server.Models.Orders;

/// <summary>Variant choices of the caller's latest fulfillment of one catalog item at one warehouse.</summary>
public class AssemblyLastPickDto
{
    public Guid CatalogItemId { get; init; }
    public Guid WarehouseId { get; init; }

    /// <summary>For a Variation: the variant picked at each nesting level, root first. Empty otherwise.</summary>
    public IReadOnlyList<AssemblyVariantStepDto> Chain { get; init; } = [];

    /// <summary>
    /// For a Bundle, or a Variation resolved to one: the variant picked at every variation slot of the
    /// composition, keyed by the slot's path (catalog item ids joined with <c>/</c>, plus the chosen variant id
    /// below each variation).
    /// </summary>
    public IReadOnlyList<AssemblyBundlePickDto> BundlePicks { get; init; } = [];
}

public class AssemblyVariantStepDto
{
    public Guid Id { get; init; }
    public CatalogItemType Type { get; init; }
}

public class AssemblyBundlePickDto
{
    public string Path { get; init; } = null!;
    public required AssemblyVariantStepDto Variant { get; init; }
}
