namespace ProjectWarehouse.Server.Infrastructure.Realtime;

/// <summary>
/// Singleton: events are published both from scoped controller services and from Quartz jobs and the
/// sync worker, which live outside any HTTP request.
/// </summary>
public interface IRealtimeNotifier
{
    ValueTask PublishAsync(RealtimeAddress address, RealtimeEvent evt, CancellationToken ct = default);
}
