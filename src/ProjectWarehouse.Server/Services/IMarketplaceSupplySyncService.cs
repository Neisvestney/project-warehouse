using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Integrations.Abstractions;

namespace ProjectWarehouse.Server.Services;

/// <summary>
/// The supply step of an order sync, driven by <see cref="IMarketplaceOrderSyncService"/>.
/// </summary>
public interface IMarketplaceSupplySyncService
{
    /// <summary>
    /// Upserts every supply the marketplace lists, then reads the composition of each open supply and of each done
    /// one not read since it closed, and points the account's postings at the supply that holds them now. An open
    /// supply the marketplace stopped listing was deleted there and is deleted here. Silently does nothing for a
    /// provider without <see cref="MarketplaceCapabilities.Supplies"/>.
    /// </summary>
    Task SyncSuppliesAsync(
        IMarketplaceProvider provider,
        MarketplaceCredentials credentials,
        MarketplaceAccount account,
        CancellationToken ct);

    /// <summary>
    /// Points the page's saved postings at the supply each one names, for postings imported after their supply's
    /// composition was last read. A posting naming no supply, or one WMS does not know, is left as it is.
    /// </summary>
    Task LinkPostingsAsync(
        Guid accountId,
        IReadOnlyList<ExternalPosting> page,
        CancellationToken ct);
}
