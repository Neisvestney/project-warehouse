namespace ProjectWarehouse.Server.Infrastructure.Realtime;

/// <summary>
/// Expiry by TTL raises no event of its own, so a lock left behind by a killed tab would keep someone
/// else's banner up until they reload. This turns expiry into a normal <c>editLockReleased</c>.
/// </summary>
public class EditLockSweeper(EditLockStore locks, IRealtimeNotifier realtime, ILogger<EditLockSweeper> logger)
    : BackgroundService
{
    private static readonly TimeSpan SweepInterval = TimeSpan.FromSeconds(10);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(SweepInterval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                foreach (var expired in locks.SweepExpired())
                    await realtime.PublishLockReleasedAsync(expired, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Edit lock sweep failed");
            }
        }
    }
}
