using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ProjectWarehouse.Server.Data;
using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Infrastructure.Marketplaces;
using Quartz;

namespace ProjectWarehouse.Server.Integrations.Sync;

/// <summary>
/// One scanning job rather than a trigger per account: the schedule then needs no mutation when
/// SyncIntervalMinutes changes, and an app restart cannot lose it.
/// </summary>
[DisallowConcurrentExecution]
public class MarketplaceSyncScanJob(
    ApplicationDbContext db,
    IMarketplaceSyncQueue queue,
    IOptionsMonitor<MarketplacesOptions> options,
    ILogger<MarketplaceSyncScanJob> logger) : IJob
{
    public const string Key = "marketplace-sync-scan";

    public async Task Execute(IJobExecutionContext context)
    {
        if (!options.CurrentValue.SyncScanEnabled)
            return;

        var ct = context.CancellationToken;
        var now = DateTime.UtcNow;

        var due = await db.MarketplaceAccounts
            .Where(a => a.IsActive)
            .Where(a => a.LastSyncAt == null
                        || a.LastSyncAt.Value.AddMinutes(a.SyncIntervalMinutes) <= now)
            .Where(a => !db.MarketplaceSyncRuns.Any(r =>
                r.MarketplaceAccountId == a.Id && r.Status == MarketplaceSyncStatus.Running))
            .Select(a => a.Id)
            .ToListAsync(ct);

        foreach (var accountId in due)
        {
            var run = new MarketplaceSyncRun
            {
                Id = Guid.NewGuid(),
                MarketplaceAccountId = accountId,
                // All refreshes what is imported (OrdersBackground) but imports nothing: new postings
                // are pulled from the FBS page by hand, where someone sees the skips.
                Scope = MarketplaceSyncScope.All,
                Status = MarketplaceSyncStatus.Running,
                QueuedAt = DateTime.UtcNow,
                TriggeredById = null,
            };
            db.MarketplaceSyncRuns.Add(run);
            await db.SaveChangesAsync(ct);

            await queue.EnqueueAsync(new MarketplaceSyncRequest(accountId, run.Id, MarketplaceSyncScope.All), ct);
            logger.LogInformation("Queued scheduled marketplace sync {SyncRunId} for account {AccountId}", run.Id, accountId);
        }
    }
}
