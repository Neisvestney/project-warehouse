using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Integrations.Abstractions;

namespace ProjectWarehouse.Server.Services;

/// <summary>
/// The order half of a sync run. Driven by <see cref="IMarketplaceSyncService"/>, which owns the run
/// lifecycle, the advisory lock and the credentials — this service only imports postings.
/// </summary>
public interface IMarketplaceOrderSyncService
{
    Task SyncOrdersAsync(
        IMarketplaceProvider provider,
        MarketplaceCredentials credentials,
        MarketplaceAccount account,
        MarketplaceSyncRun run,
        CancellationToken ct);

    /// <summary>
    /// The unattended half: it refreshes what is already imported and imports marketplace-fulfilled
    /// postings, which are external and so cannot pile up skips. Safe on a background interval, and also
    /// the second phase of <see cref="SyncOrdersAsync"/>.
    /// </summary>
    Task SyncOrdersBackgroundAsync(
        IMarketplaceProvider provider,
        MarketplaceCredentials credentials,
        MarketplaceAccount account,
        MarketplaceSyncRun run,
        CancellationToken ct);

    /// <summary>
    /// One-off history import over <see cref="MarketplaceSyncRun.BackfillSince"/>..<c>BackfillTo</c>:
    /// everything the marketplace has already finished with that WMS does not know, as external orders.
    /// Postings WMS already has are left untouched.
    /// </summary>
    Task SyncOrdersBackfillAsync(
        IMarketplaceProvider provider,
        MarketplaceCredentials credentials,
        MarketplaceAccount account,
        MarketplaceSyncRun run,
        CancellationToken ct);
}
