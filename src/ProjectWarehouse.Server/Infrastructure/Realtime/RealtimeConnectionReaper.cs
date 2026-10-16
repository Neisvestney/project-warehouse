namespace ProjectWarehouse.Server.Infrastructure.Realtime;

/// <summary>
/// Closes streams whose client stopped heartbeating. A dev proxy or a mobile NAT can outlive the browser
/// and keep the socket writable, so a dropped tab is otherwise indistinguishable from an idle one — its
/// presence and its edit locks would sit in the registries until the process restarts.
/// </summary>
public class RealtimeConnectionReaper(RealtimeConnectionManager connections, ILogger<RealtimeConnectionReaper> logger)
    : BackgroundService
{
    private static readonly TimeSpan SweepInterval = TimeSpan.FromSeconds(10);

    /// <summary>Generous against the client's 20 s interval: background tabs are throttled to a minute.</summary>
    public static readonly TimeSpan ConnectionTtl = TimeSpan.FromSeconds(90);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(SweepInterval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                connections.AbortStale(ConnectionTtl);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Realtime connection sweep failed");
            }
        }
    }
}
