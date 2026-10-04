using Microsoft.EntityFrameworkCore;
using ProjectWarehouse.Server.Data;
using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Infrastructure.Observability;

namespace ProjectWarehouse.Server.Services;

public class ExternalOrderRebindService(ApplicationDbContext db) : IExternalOrderRebindService
{
    private const int OrdersPerBatch = 500;

    public async Task<int> BindUnmappedAsync(IReadOnlyCollection<Guid> cardIds, CancellationToken ct)
    {
        if (cardIds.Count == 0)
            return 0;

        var changes = await Plan(Rebindable()
                .Where(i => i.CatalogItemId == null && cardIds.Contains(i.MarketplaceCardId!.Value)))
            .ToListAsync(ct);

        return (await ApplyAsync(changes, ct)).Count;
    }

    public async Task<IReadOnlyList<ExternalOrderRebindChange>> PlanRebindAsync(
        IReadOnlyCollection<Guid> accountIds, DateTime since, CancellationToken ct) =>
        await Plan(Rebindable()
                .Where(i => accountIds.Contains(i.MarketplaceCard!.MarketplaceAccountId)
                            && i.Order.EffectiveDate >= since))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<ExternalOrderRebindChange>> ApplyAsync(
        IReadOnlyList<ExternalOrderRebindChange> changes, CancellationToken ct)
    {
        if (changes.Count == 0)
            return [];

        var applied = new List<ExternalOrderRebindChange>();
        await db.Database.ExecuteInTransactionAsync("marketplaces.external_orders.rebind", async () =>
        {
            foreach (var batch in changes.GroupBy(c => c.OrderId).Chunk(OrdersPerBatch))
                applied.AddRange(await ApplyBatchAsync(batch, ct));
        }, ct);

        return applied;
    }

    private async Task<List<ExternalOrderRebindChange>> ApplyBatchAsync(
        IGrouping<Guid, ExternalOrderRebindChange>[] batch, CancellationToken ct)
    {
        var orderIds = batch.Select(g => g.Key).ToList();
        var itemIds = batch.SelectMany(g => g).Select(c => c.ItemId).ToArray();

        // the plan was read outside the transaction: lock the lines, and below skip any a concurrent
        // rebind already moved, or its quantity would be shifted onto the component twice
        var items = await db.OrderMarketplaceItems
            .FromSql($@"SELECT * FROM ""OrderMarketplaceItems"" WHERE ""Id"" = ANY({itemIds}) FOR UPDATE")
            .ToDictionaryAsync(i => i.Id, ct);
        var boxes = (await db.OrderBoxes
                .Include(b => b.Components)
                .Where(b => orderIds.Contains(b.OrderId))
                .ToListAsync(ct))
            .ToLookup(b => b.OrderId);

        var applied = new List<ExternalOrderRebindChange>();
        foreach (var order in batch)
        {
            var orderBoxes = boxes[order.Key].ToList();
            foreach (var change in order)
            {
                if (!items.TryGetValue(change.ItemId, out var item) || item.CatalogItemId != change.OldCatalogItemId)
                    continue;

                item.CatalogItemId = change.NewCatalogItemId;
                MoveComponent(orderBoxes, change);
                applied.Add(change);
            }
        }

        await db.SaveChangesAsync(ct);
        if (applied.Count == 0)
            return applied;

        var appliedIds = applied.Select(c => c.ItemId).ToList();

        // returns and accruals copy the line's catalog item when they are linked, so they follow it here
        await db.MarketplaceReturns
            .Where(r => r.OrderMarketplaceItemId != null && appliedIds.Contains(r.OrderMarketplaceItemId.Value))
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.CatalogItemId, r => r.OrderMarketplaceItem!.CatalogItemId), ct);
        await db.MarketplaceAccruals
            .Where(a => a.OrderMarketplaceItemId != null && appliedIds.Contains(a.OrderMarketplaceItemId.Value))
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.CatalogItemId, a => a.OrderMarketplaceItem!.CatalogItemId), ct);

        return applied;
    }

    /// <summary>
    /// Shifts the line's quantity from the old component to the new one, leaving the rest of the box as it is.
    /// Components stay collapsed by catalog item, as the import builds them.
    /// </summary>
    private void MoveComponent(List<OrderBox> boxes, ExternalOrderRebindChange change)
    {
        if (boxes.Count == 0)
            return;

        if (change.OldCatalogItemId is { } oldId
            && boxes.SelectMany(b => b.Components)
                .FirstOrDefault(c => c.CatalogItemId == oldId && db.Entry(c).State != EntityState.Deleted) is { } old)
        {
            old.Quantity -= change.Quantity;
            if (old.Quantity <= 0)
                db.OrderBoxComponents.Remove(old);
        }

        var target = boxes.SelectMany(b => b.Components)
            .FirstOrDefault(c => c.CatalogItemId == change.NewCatalogItemId && db.Entry(c).State != EntityState.Deleted);
        if (target is not null)
        {
            target.Quantity += change.Quantity;
            return;
        }

        var box = boxes[0];
        var component = new OrderBoxComponent
        {
            Id = Guid.NewGuid(),
            OrderBoxId = box.Id,
            CatalogItemId = change.NewCatalogItemId,
            Quantity = change.Quantity,
        };
        box.Components.Add(component);
        db.OrderBoxComponents.Add(component);
    }

    /// <summary>Lines of external orders whose snapshot disagrees with their card's current mapping.</summary>
    private IQueryable<OrderMarketplaceItem> Rebindable() =>
        db.OrderMarketplaceItems
            .Where(i => i.Order.IsExternal
                        && i.MarketplaceCard != null
                        && i.MarketplaceCard.CatalogItemId != null
                        && i.CatalogItemId != i.MarketplaceCard.CatalogItemId);

    private static IQueryable<ExternalOrderRebindChange> Plan(IQueryable<OrderMarketplaceItem> items) =>
        items
            .OrderBy(i => i.Order.Number)
            .Select(i => new ExternalOrderRebindChange(
                i.Id, i.OrderId, i.Order.Number, i.MarketplaceCardId!.Value, i.MarketplaceCard!.MarketplaceAccountId,
                i.Quantity, i.CatalogItemId, i.MarketplaceCard.CatalogItemId!.Value));
}
