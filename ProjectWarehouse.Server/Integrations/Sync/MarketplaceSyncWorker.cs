using Microsoft.EntityFrameworkCore;
using ProjectWarehouse.Server.Data;
using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Infrastructure;
using ProjectWarehouse.Server.Models;
using ProjectWarehouse.Server.Services;

namespace ProjectWarehouse.Server.Integrations.Sync;

/// <summary>
/// Drains the sync queue. Runs outside the request scope because POST /sync answers 202 immediately;
/// a BackgroundService rather than Task.Run so shutdown is observed instead of silently dropping work.
/// </summary>
public class MarketplaceSyncWorker(
    IMarketplaceSyncQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<MarketplaceSyncWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await ReconcileInterruptedRunsAsync(stoppingToken);

        await foreach (var request in queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var sync = scope.ServiceProvider.GetRequiredService<IMarketplaceSyncService>();
                await sync.RunAsync(request, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Marketplace sync worker failed on run {SyncRunId}", request.SyncRunId);
            }
        }
    }

    /// <summary>
    /// A run left in Running by a crash would block the account forever — the UI check and the
    /// scheduler both refuse to start a second one. The account's own summary is rolled back too,
    /// otherwise it keeps advertising the outcome of the run before the crash.
    /// </summary>
    private async Task ReconcileInterruptedRunsAsync(CancellationToken ct)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var error = AppProblems.MakeError(ErrorCode.MarketplaceSyncInterrupted,
                "The sync was interrupted by an application shutdown.");
            var now = DateTime.UtcNow;

            var accountIds = await db.MarketplaceSyncRuns
                .Where(r => r.Status == MarketplaceSyncStatus.Running)
                .Select(r => r.MarketplaceAccountId)
                .Distinct()
                .ToListAsync(ct);

            // TEMP-TRANSLATION-TEST
            // if (accountIds.Count == 0) return;

            var affected = await db.MarketplaceSyncRuns
                .Where(r => r.Status == MarketplaceSyncStatus.Running)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(r => r.Status, MarketplaceSyncStatus.Failed)
                    .SetProperty(r => r.Error, error)
                    .SetProperty(r => r.FinishedAt, now), ct);

            // mirrors what MarketplaceSyncService.FailAsync writes, including LastSyncAt — the scan job
            // reads it, so a crashed account waits out its normal interval instead of retrying at once
            await db.MarketplaceAccounts
                .Where(a => accountIds.Contains(a.Id))
                .ExecuteUpdateAsync(s => s
                    .SetProperty(a => a.LastSyncStatus, MarketplaceSyncStatus.Failed)
                    .SetProperty(a => a.LastSyncError, error)
                    .SetProperty(a => a.LastSyncAt, now), ct);

            logger.LogWarning("Marked {Count} interrupted marketplace sync run(s) across {Accounts} account(s) as failed",
                affected, accountIds.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to reconcile interrupted marketplace sync runs");
        }
    }
}
