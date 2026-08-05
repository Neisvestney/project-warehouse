using ProjectWarehouse.Server.Integrations.Sync;

namespace ProjectWarehouse.Server.Services;

public interface IMarketplaceSyncService
{
    /// <summary>Runs a queued sync to completion. Never throws — failures land in the run's Error.</summary>
    Task RunAsync(MarketplaceSyncRequest request, CancellationToken ct);

    /// <summary>Manual auto-mapping over the whole account. Returns how many cards got mapped.</summary>
    Task<int> AutoMapAccountAsync(Guid accountId, CancellationToken ct);
}
