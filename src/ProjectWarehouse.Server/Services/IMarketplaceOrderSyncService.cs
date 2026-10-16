using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Integrations.Abstractions;

namespace ProjectWarehouse.Server.Services;

/// <summary>
/// The order half of a sync run. Driven by <see cref="IMarketplaceSyncService"/>, which owns the run
/// lifecycle, the advisory lock and the credentials — this service only imports postings.
/// </summary>
public interface IMarketplaceOrderSyncService
{
    /// <summary>
    /// The steps of <see cref="SyncOrdersBackgroundAsync"/> with the working import between them, right after FBS
    /// discovery and catch-up: FBS drafts the marketplace has packed become working orders, or land in the skipped
    /// list when their catalog or warehouse is not mapped or discovery did not see them this run.
    /// </summary>
    Task SyncOrdersAsync(
        IMarketplaceProvider provider,
        MarketplaceCredentials credentials,
        MarketplaceAccount account,
        MarketplaceSyncRun run,
        CancellationToken ct);

    /// <summary>
    /// The unattended half: it imports FBS postings handed to the seller as drafts, refreshes what is already
    /// imported, imports marketplace-fulfilled and past-the-warehouse postings as external orders, and imports
    /// returns — none of which can pile up skips. Safe on a background interval; <see cref="SyncOrdersAsync"/>
    /// runs the same steps.
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
    /// Postings WMS already has are left untouched. Returns handed back within the period follow, then the
    /// accruals of every day of it.
    /// </summary>
    Task SyncOrdersBackfillAsync(
        IMarketplaceProvider provider,
        MarketplaceCredentials credentials,
        MarketplaceAccount account,
        MarketplaceSyncRun run,
        CancellationToken ct);
}
