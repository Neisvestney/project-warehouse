using ProjectWarehouse.Server.Integrations.Sync;

namespace ProjectWarehouse.Server.Services;

public interface IMarketplaceSyncService
{
    /// <summary>Runs a queued sync to completion. Never throws — failures land in the run's Error.</summary>
    Task RunAsync(MarketplaceSyncRequest request, CancellationToken ct);

    /// <summary>
    /// Manual auto-mapping over whole accounts: the cards whose mapping would change, ordered by account and
    /// offer id. Nothing is written — the caller applies the changes, or doesn't on a dry run.
    /// </summary>
    Task<IReadOnlyList<MarketplaceAutoMapChange>> PlanAutoMapAsync(
        IReadOnlyCollection<Guid> accountIds, MarketplaceAutoMapScope scope, CancellationToken ct);
}
