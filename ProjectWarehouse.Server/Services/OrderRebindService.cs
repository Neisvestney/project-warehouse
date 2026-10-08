using Microsoft.EntityFrameworkCore;
using ProjectWarehouse.Server.Data;
using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Infrastructure;
using ProjectWarehouse.Server.Infrastructure.Observability;
using ProjectWarehouse.Server.Models;

namespace ProjectWarehouse.Server.Services;

public class OrderRebindService(ApplicationDbContext db, IEntityLockService locks) : IOrderRebindService
{
    private const int OrdersPerBatch = 500;

    public async Task<WorkingOrdersRebindResult> RebindWorkingOrdersAsync(
        IReadOnlyCollection<Order> orders, CancellationToken ct)
    {
        var failures = new Dictionary<Guid, AppFieldError>();
        foreach (var order in orders)
        {
            if (order.IsExternal)
                failures[order.Id] = AppProblems.MakeError(ErrorCode.OrderIsExternal,
                    "This order was imported from the marketplace and is not handled by WMS.");
            else if (order.Type != OrderType.FBS)
                failures[order.Id] = AppProblems.MakeError(ErrorCode.OrderNotFbs,
                    "Only FBS orders follow their cards' mapping.");
            else if (order.Status != OrderStatus.Confirmed)
                failures[order.Id] = AppProblems.MakeError(ErrorCode.OrderNotConfirmed,
                    "Only a Confirmed order can be rebound.");
        }

        var candidateIds = orders.Select(o => o.Id).Where(id => !failures.ContainsKey(id)).ToList();
        if (candidateIds.Count == 0)
            return new WorkingOrdersRebindResult([], failures);

        // a line without a mapping has no item to move to, and a working order cannot hold an unknown one
        var unmapped = await db.OrderMarketplaceItems
            .Where(i => candidateIds.Contains(i.OrderId)
                        && (i.MarketplaceCard == null || i.MarketplaceCard.CatalogItemId == null))
            .Select(i => new { i.OrderId, OfferId = i.MarketplaceCard != null ? i.MarketplaceCard.OfferId : null })
            .ToListAsync(ct);
        foreach (var order in unmapped.GroupBy(i => i.OrderId))
            failures[order.Key] = AppProblems.MakeError(ErrorCode.MarketplaceOrderCardNotMapped,
                "Some cards of the order are not mapped to the catalog.",
                new Dictionary<string, object>
                {
                    ["offerIds"] = string.Join(", ", order.Select(i => i.OfferId).OfType<string>().Distinct()),
                });

        var rebindableIds = candidateIds.Where(id => !failures.ContainsKey(id)).ToList();
        var changes = await Plan(Mismatched().Where(i => rebindableIds.Contains(i.OrderId))).ToListAsync(ct);
        var applied = await ApplyAsync(changes, ct);

        return new WorkingOrdersRebindResult([.. applied.Select(c => c.OrderId).Distinct()], failures);
    }

    public async Task<int> BindUnmappedAsync(IReadOnlyCollection<Guid> cardIds, CancellationToken ct,
        TimeSpan? lockTimeout = null)
    {
        if (cardIds.Count == 0)
            return 0;

        var changes = await Plan(Rebindable()
                .Where(i => i.CatalogItemId == null && cardIds.Contains(i.MarketplaceCardId!.Value)))
            .ToListAsync(ct);

        return (await ApplyAsync(changes, ct, lockTimeout)).Count;
    }

    public async Task<IReadOnlyList<OrderRebindChange>> PlanRebindAsync(
        IReadOnlyCollection<Guid> accountIds, DateTime since, CancellationToken ct) =>
        await Plan(Rebindable()
                .Where(i => accountIds.Contains(i.MarketplaceCard!.MarketplaceAccountId)
                            && i.Order.EffectiveDate >= since))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<OrderRebindChange>> ApplyAsync(
        IReadOnlyList<OrderRebindChange> changes, CancellationToken ct, TimeSpan? lockTimeout = null)
    {
        if (changes.Count == 0)
            return [];

        var applied = new List<OrderRebindChange>();
        await db.Database.ExecuteInTransactionAsync("marketplaces.external_orders.rebind", async () =>
        {
            // boxes are edited under the order lock elsewhere, so take it before the line locks below
            await locks.LockManyAsync<Order>(changes.Select(c => c.OrderId), ct, lockTimeout);

            foreach (var batch in changes.GroupBy(c => c.OrderId).Chunk(OrdersPerBatch))
                applied.AddRange(await ApplyBatchAsync(batch, ct));
        }, ct);

        return applied;
    }

    private async Task<List<OrderRebindChange>> ApplyBatchAsync(
        IGrouping<Guid, OrderRebindChange>[] batch, CancellationToken ct)
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
                .OrderBy(b => b.Id)
                .ToListAsync(ct))
            .ToLookup(b => b.OrderId);

        var applied = new List<OrderRebindChange>();
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
    /// Shifts the line's quantity from the old item to the new one box by box, so units split over several
    /// boxes are replaced where they lie; whatever the old item lacked goes into the first box. Components stay
    /// collapsed by catalog item within a box, as the import builds them.
    /// </summary>
    private void MoveComponent(List<OrderBox> boxes, OrderRebindChange change)
    {
        if (boxes.Count == 0)
            return;

        var left = change.Quantity;
        if (change.OldCatalogItemId is { } oldId)
        {
            foreach (var box in boxes)
            {
                if (left <= 0)
                    return;
                if (LiveComponent(box, oldId) is not { } old)
                    continue;

                var taken = Math.Min(left, old.Quantity);
                old.Quantity -= taken;
                if (old.Quantity <= 0)
                    db.OrderBoxComponents.Remove(old);

                AddToBox(box, change.NewCatalogItemId, taken);
                left -= taken;
            }
        }

        if (left > 0)
            AddToBox(boxes[0], change.NewCatalogItemId, left);
    }

    private OrderBoxComponent? LiveComponent(OrderBox box, Guid catalogItemId) =>
        box.Components.FirstOrDefault(c => c.CatalogItemId == catalogItemId && db.Entry(c).State != EntityState.Deleted);

    private void AddToBox(OrderBox box, Guid catalogItemId, int quantity)
    {
        if (LiveComponent(box, catalogItemId) is { } target)
        {
            target.Quantity += quantity;
            return;
        }

        var component = new OrderBoxComponent
        {
            Id = Guid.NewGuid(),
            OrderBoxId = box.Id,
            CatalogItemId = catalogItemId,
            Quantity = quantity,
        };
        box.Components.Add(component);
        db.OrderBoxComponents.Add(component);
    }

    /// <summary>Lines whose snapshot disagrees with their card's current mapping.</summary>
    private IQueryable<OrderMarketplaceItem> Mismatched() =>
        db.OrderMarketplaceItems
            .Where(i => i.MarketplaceCard != null
                        && i.MarketplaceCard.CatalogItemId != null
                        && i.CatalogItemId != i.MarketplaceCard.CatalogItemId);

    /// <summary>Mismatched lines of external orders — the only ones that follow the mapping without being asked.</summary>
    private IQueryable<OrderMarketplaceItem> Rebindable() => Mismatched().Where(i => i.Order.IsExternal);

    private static IQueryable<OrderRebindChange> Plan(IQueryable<OrderMarketplaceItem> items) =>
        items
            .OrderBy(i => i.Order.Number)
            .Select(i => new OrderRebindChange(
                i.Id, i.OrderId, i.Order.Number, i.MarketplaceCardId!.Value, i.MarketplaceCard!.MarketplaceAccountId,
                i.Quantity, i.CatalogItemId, i.MarketplaceCard.CatalogItemId!.Value));
}
