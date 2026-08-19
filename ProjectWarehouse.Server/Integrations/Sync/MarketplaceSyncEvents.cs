using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Infrastructure.Realtime;

namespace ProjectWarehouse.Server.Integrations.Sync;

/// <summary>
/// Sync events are addressed to the account's watchers, not the run's: the client knows the account id
/// before a run exists, and runs started by the scheduler or another user would otherwise be invisible.
/// </summary>
public static class MarketplaceSyncEvents
{
    public static ValueTask PublishProgressAsync(this IRealtimeNotifier notifier, MarketplaceSyncRun run,
        CancellationToken ct = default) =>
        notifier.PublishAsync(AddressOf(run), new RealtimeEvent
        {
            Payload = new MarketplaceSyncProgressPayload
            {
                AccountId = run.MarketplaceAccountId,
                SyncRunId = run.Id,
            },
        }, ct);

    public static ValueTask PublishFinishedAsync(this IRealtimeNotifier notifier, MarketplaceSyncRun run,
        CancellationToken ct = default) =>
        notifier.PublishAsync(AddressOf(run), new RealtimeEvent
        {
            Payload = new MarketplaceSyncFinishedPayload
            {
                AccountId = run.MarketplaceAccountId,
                SyncRunId = run.Id,
                Status = run.Status,
            },
        }, ct);

    private static RealtimeAddress AddressOf(MarketplaceSyncRun run) =>
        RealtimeAddress.ToWatchers(AppEntityType.MarketplaceAccount, run.MarketplaceAccountId);
}
