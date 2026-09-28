using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Integrations.Abstractions;

namespace ProjectWarehouse.Server.Services;

/// <summary>
/// Reads the seller's accruals into <see cref="MarketplaceAccrual"/>. Both methods silently do nothing for a
/// provider without <see cref="MarketplaceCapabilities.Accruals"/>.
/// </summary>
public interface IMarketplaceAccrualSyncService
{
    /// <summary>
    /// Reads the days since <see cref="MarketplaceAccount.AccrualsSyncedAt"/> — over the whole overlap once a
    /// day, see <see cref="MarketplaceAccount.AccrualsFullPassAt"/> — then retries linking accruals whose
    /// posting was not imported yet.
    /// </summary>
    Task SyncAccrualsAsync(
        IMarketplaceProvider provider,
        MarketplaceCredentials credentials,
        MarketplaceAccount account,
        MarketplaceSyncRun run,
        CancellationToken ct);

    /// <summary>Accruals of every day of the period, for the history import. Leaves the marks alone.</summary>
    Task SyncAccrualsBackfillAsync(
        IMarketplaceProvider provider,
        MarketplaceCredentials credentials,
        MarketplaceAccount account,
        MarketplaceSyncRun run,
        DateTime since,
        DateTime to,
        CancellationToken ct);
}
