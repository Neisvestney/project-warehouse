using ProjectWarehouse.Server.Domain;

namespace ProjectWarehouse.Server.Services;

/// <summary>Which already-mapped cards a manual auto-mapping run may touch. Unmapped cards are always in scope.</summary>
public record MarketplaceAutoMapScope(bool OverwriteAuto, bool OverwriteManual, bool ClearUnmatched);

/// <summary>
/// One card whose mapping a run would change. Computed without touching the card, so a dry run and a real
/// run render the same list; <see cref="Apply"/> writes it onto the tracked entity.
/// </summary>
public class MarketplaceAutoMapChange
{
    public required MarketplaceCard Card { get; init; }
    public required Guid? OldCatalogItemId { get; init; }
    public required MarketplaceMappingSource? OldMappingSource { get; init; }
    public required Guid? NewCatalogItemId { get; init; }
    public required MarketplaceMappingSource? NewMappingSource { get; init; }

    public void Apply(DateTime now)
    {
        Card.CatalogItemId = NewCatalogItemId;
        Card.MappingSource = NewMappingSource;
        Card.MappedAt = NewCatalogItemId is null ? null : now;
    }
}
