using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ProjectWarehouse.Server.Data;
using ProjectWarehouse.Server.Domain;
using Quartz;

namespace ProjectWarehouse.Server.Infrastructure.Marketplaces;

/// <summary>
/// Drops the reference to cached labels older than <c>Labels:CacheTtlDays</c>; the files themselves become
/// orphans and <see cref="Files.DataFilesGcJob"/> removes them. An awaiting_deliver posting keeps its parcels' labels:
/// it is the one about to be packed, and it is also the only status the marketplace can reprint for.
/// </summary>
[DisallowConcurrentExecution]
public class MarketplaceLabelsGcJob(
    ApplicationDbContext db,
    IOptions<MarketplacesOptions> options,
    ILogger<MarketplaceLabelsGcJob> logger) : IJob
{
    public const string Key = "marketplace-labels-gc";

    public async Task Execute(IJobExecutionContext context)
    {
        var ct = context.CancellationToken;

        // No advisory lock: the update is idempotent and deletes nothing, so a second instance is a no-op
        var cutoff = DateTime.UtcNow.AddDays(-options.Value.Labels.CacheTtlDays);
        var released = await db.MarketplaceParcels
            .Where(p => p.LabelFileId != null
                        && p.LabelFetchedAt < cutoff
                        && p.MarketplaceOrder.Status != MarketplaceOrderStatus.AwaitingDeliver)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.LabelFileId, (Guid?)null)
                .SetProperty(p => p.LabelKind, (LabelKind?)null)
                .SetProperty(p => p.LabelFetchedAt, (DateTime?)null), ct);

        if (released > 0)
            logger.LogInformation("Marketplace labels GC released {RowCount} cached labels", released);
    }
}
