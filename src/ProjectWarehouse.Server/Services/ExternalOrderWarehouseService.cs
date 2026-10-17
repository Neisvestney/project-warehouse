using Microsoft.EntityFrameworkCore;
using ProjectWarehouse.Server.Data;
using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Infrastructure;
using ProjectWarehouse.Server.Infrastructure.Observability;

namespace ProjectWarehouse.Server.Services;

public class ExternalOrderWarehouseService(ApplicationDbContext db, IEntityLockService locks)
    : IExternalOrderWarehouseService
{
    private const int OrdersPerBatch = 500;

    public async Task<IReadOnlyList<Guid>> BindUnmappedAsync(Guid accountId, string warehouseExternalId,
        Guid warehouseId, CancellationToken ct, TimeSpan? lockTimeout = null)
    {
        // an order that turns unbound after this read waits for the next binding
        var candidateIds = await Unbound(accountId, warehouseExternalId)
            .OrderBy(o => o.Id)
            .Select(o => o.Id)
            .ToListAsync(ct);
        if (candidateIds.Count == 0)
            return [];

        var bound = new List<Guid>();
        await db.Database.ExecuteInTransactionAsync("marketplaces.external_orders.bind_warehouse", async () =>
        {
            // batches go in id order, so the locks are still taken in id order across the whole run
            foreach (var batch in candidateIds.Chunk(OrdersPerBatch))
            {
                await locks.LockManyAsync<Order>(batch, ct, lockTimeout);

                // the filters again: an order bound while the locks were being taken keeps its warehouse
                var ids = await Unbound(accountId, warehouseExternalId)
                    .Where(o => batch.Contains(o.Id))
                    .Select(o => o.Id)
                    .ToListAsync(ct);
                if (ids.Count == 0)
                    continue;

                await db.Orders
                    .Where(o => ids.Contains(o.Id))
                    .ExecuteUpdateAsync(s => s.SetProperty(o => o.WarehouseId, warehouseId), ct);
                bound.AddRange(ids);
            }
        }, ct);

        return bound;
    }

    private IQueryable<Order> Unbound(Guid accountId, string warehouseExternalId) =>
        db.Orders.Where(o => o.IsExternal
                             && o.Type == OrderType.FBS
                             && o.WarehouseId == null
                             && o.MarketplaceOrder!.MarketplaceAccountId == accountId
                             && o.MarketplaceOrder.WarehouseExternalId == warehouseExternalId);
}
