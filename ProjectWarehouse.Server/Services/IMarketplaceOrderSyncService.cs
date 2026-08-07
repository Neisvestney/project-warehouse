using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Integrations.Abstractions;

namespace ProjectWarehouse.Server.Services;

/// <summary>
/// The FBS half of a sync run. Driven by <see cref="IMarketplaceSyncService"/>, which owns the run
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
}
