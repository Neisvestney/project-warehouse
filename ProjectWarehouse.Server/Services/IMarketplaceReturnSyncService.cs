using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Integrations.Abstractions;

namespace ProjectWarehouse.Server.Services;

/// <summary>
/// The return step of an order sync, driven by <see cref="IMarketplaceOrderSyncService"/> once the postings
/// the returns belong to are in. Both methods silently do nothing for a provider without
/// <see cref="MarketplaceCapabilities.Returns"/>.
/// </summary>
public interface IMarketplaceReturnSyncService
{
    /// <summary>
    /// Reads status changes since <see cref="MarketplaceAccount.ReturnsSyncedAt"/>, then every return that
    /// ever had a compensation, then retries linking returns whose posting was not imported yet.
    /// </summary>
    Task SyncReturnsAsync(
        IMarketplaceProvider provider,
        MarketplaceCredentials credentials,
        MarketplaceAccount account,
        MarketplaceSyncRun run,
        CancellationToken ct);

    /// <summary>Returns handed back within the period, for the history import. Leaves the mark alone.</summary>
    Task SyncReturnsBackfillAsync(
        IMarketplaceProvider provider,
        MarketplaceCredentials credentials,
        MarketplaceAccount account,
        MarketplaceSyncRun run,
        DateTime since,
        DateTime to,
        CancellationToken ct);
}
