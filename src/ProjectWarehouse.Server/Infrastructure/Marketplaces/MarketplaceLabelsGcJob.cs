using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ProjectWarehouse.Server.Data;
using ProjectWarehouse.Server.Domain;
using Quartz;

namespace ProjectWarehouse.Server.Infrastructure.Marketplaces;

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
                        && (p.MarketplaceOrder.Status == MarketplaceOrderStatus.Delivered
                            || p.MarketplaceOrder.Status == MarketplaceOrderStatus.Cancelled))
            .Where(p => p.MarketplaceOrder.Order.IsExternal
                        || p.MarketplaceOrder.Order.Status == OrderStatus.Shipped)
            // The maximum is expired only when every available event date is expired.
            .Where(p => (p.MarketplaceOrder.Order.ShippedAt == null || p.MarketplaceOrder.Order.ShippedAt < cutoff)
                        && (p.MarketplaceOrder.DeliveredAt == null || p.MarketplaceOrder.DeliveredAt < cutoff)
                        && (p.MarketplaceOrder.CancelledAt == null || p.MarketplaceOrder.CancelledAt < cutoff))
            .Where(p => p.MarketplaceOrder.Order.ShippedAt != null
                        || p.MarketplaceOrder.DeliveredAt != null
                        || p.MarketplaceOrder.CancelledAt != null
                        || p.LabelFetchedAt < cutoff)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.LabelFileId, (Guid?)null)
                .SetProperty(p => p.LabelKind, (LabelKind?)null)
                .SetProperty(p => p.LabelFetchedAt, (DateTime?)null), ct);

        if (released > 0)
            logger.LogInformation("Marketplace labels GC released {RowCount} cached labels", released);
    }
}
