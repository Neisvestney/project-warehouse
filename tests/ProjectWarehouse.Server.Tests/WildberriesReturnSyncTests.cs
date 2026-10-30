using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ProjectWarehouse.Server.Data;
using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Infrastructure.Marketplaces;
using ProjectWarehouse.Server.Infrastructure.Realtime;
using ProjectWarehouse.Server.Integrations.Sync;
using ProjectWarehouse.Server.Integrations.Wildberries;
using ProjectWarehouse.Server.Services;

namespace ProjectWarehouse.Server.Tests;

public class WildberriesReturnSyncTests(TokenDatabase database) : IClassFixture<TokenDatabase>
{
    [PostgresFact]
    public async Task Reconcile_SeparatesAttemptsRepairsFeesAndKeepsBuyerReturnAndCatalogSnapshots()
    {
        await using var scope = database.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var account = Account();
        var original = Item("Original");
        var later = Item("Later");
        var current = Item("Current");
        var card = new MarketplaceCard
        {
            Id = Guid.NewGuid(), MarketplaceAccount = account, ExternalId = "card", Sku = "sku",
            OfferId = "offer", Name = "Card", CatalogItem = current,
        };
        var oldOrder = Order(account, "first", OrderType.FBS, "unit", card, original);
        var newOrder = Order(account, "second", OrderType.FboPosting, null, card, later);
        var first = Attempt(account, "first-attempt", new(2026, 7, 15), oldOrder);
        var second = Attempt(account, "second-attempt", new(2026, 9, 25), oldOrder);
        var buyer = new MarketplaceReturn
        {
            Id = Guid.NewGuid(), MarketplaceAccount = account, ExternalId = "unit", ExemplarId = "unit",
            PostingNumber = "first", Order = oldOrder, OrderMarketplaceItem = oldOrder.MarketplaceItems.Single(),
            CatalogItem = original, OfferId = "offer", Sku = "sku", Quantity = 1,
            Kind = MarketplaceReturnKind.CustomerReturn, SaleReversedOn = new(2026, 7, 1), Price = 3556,
        };
        var oldFee = Journal(account, oldOrder, new(2026, 7, 20), first.SourceExternalId);
        var newFee = Journal(account, oldOrder, new(2026, 10, 15), second.SourceExternalId);
        var reimbursement = Journal(account, oldOrder, new(2026, 9, 24));
        reimbursement.RawTypeId = "Expense reimbursement";
        db.AddRange(first, second, buyer, oldFee, newFee,
            reimbursement, Journal(account, oldOrder, new(2026, 7, 1)),
            Journal(account, newOrder, new(2026, 9, 17)));
        await db.SaveChangesAsync();

        var service = Service(db);
        var run = new MarketplaceSyncRun();
        await service.ReconcileWildberriesAsync(account.Id, run, CancellationToken.None);
        db.ChangeTracker.Clear();

        var attempts = await db.MarketplaceReturns.Where(r => r.MarketplaceAccountId == account.Id)
            .ToDictionaryAsync(r => r.ExternalId);
        var savedFirst = attempts[first.ExternalId];
        var savedSecond = attempts[second.ExternalId];
        Assert.Equal(oldOrder.Id, savedFirst.OrderId);
        Assert.Equal(newOrder.Id, savedSecond.OrderId);
        Assert.Equal(newOrder.MarketplaceItems.Single().Id, savedSecond.OrderMarketplaceItemId);
        Assert.Equal(later.Id, savedSecond.CatalogItemId);
        Assert.Equal(MarketplaceReturnScheme.Fbo, savedSecond.Scheme);
        Assert.Equal(oldOrder.Id, (await db.MarketplaceAccruals.SingleAsync(a => a.Id == oldFee.Id)).OrderId);
        var savedFee = await db.MarketplaceAccruals.SingleAsync(a => a.Id == newFee.Id);
        Assert.Equal(newOrder.Id, savedFee.OrderId);
        Assert.Equal(later.Id, savedFee.CatalogItemId);
        var savedBuyer = attempts["unit"];
        Assert.Equal(oldOrder.Id, savedBuyer.OrderId);
        Assert.Equal(original.Id, savedBuyer.CatalogItemId);
        Assert.Equal(new DateOnly(2026, 7, 1), savedBuyer.SaleReversedOn);
        Assert.Equal(3556m, savedBuyer.Price);
        Assert.Equal(1, attempts.Values.Where(r => r.IsCountedAsReturn).Sum(r => r.Quantity));

        var repeated = new MarketplaceSyncRun();
        await service.ReconcileWildberriesAsync(account.Id, repeated, CancellationToken.None);
        Assert.Equal(0, repeated.ReturnsUpdated);
        Assert.Equal(0, repeated.AccrualsUpdated);
        Assert.Equal(3, await db.MarketplaceReturns.CountAsync(r => r.MarketplaceAccountId == account.Id));
    }

    [PostgresFact]
    public async Task Reconcile_AmbiguousHistoryClearsOldLinksAndDoesNotUseSellerFeesOrOldLabelAsEvidence()
    {
        await using var scope = database.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var account = Account();
        var firstOrder = Order(account, "first", OrderType.FBS, "unit");
        var secondOrder = Order(account, "second", OrderType.FboPosting, null);
        var attempt = Attempt(account, "attempt", new(2026, 9, 25), firstOrder);
        var fee = Journal(account, firstOrder, new(2026, 9, 24), attempt.SourceExternalId);
        db.AddRange(attempt, fee, Journal(account, firstOrder, new(2026, 9, 17)),
            Journal(account, secondOrder, new(2026, 9, 17)));
        await db.SaveChangesAsync();

        await Service(db).ReconcileWildberriesAsync(account.Id, new MarketplaceSyncRun(), CancellationToken.None);
        db.ChangeTracker.Clear();

        var saved = await db.MarketplaceReturns.SingleAsync(r => r.Id == attempt.Id);
        Assert.Null(saved.OrderId);
        Assert.Null(saved.OrderMarketplaceItemId);
        Assert.Null(saved.CatalogItemId);
        Assert.Equal("", saved.PostingNumber);
        Assert.Equal(MarketplaceReturnScheme.Unknown, saved.Scheme);
        Assert.Null((await db.MarketplaceAccruals.SingleAsync(a => a.Id == fee.Id)).OrderId);
    }

    [PostgresFact]
    public async Task Reconcile_EmptySkuKeepsConfirmedItemOfUnchangedOrderWithSeveralItems()
    {
        await using var scope = database.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var account = Account();
        var snapshot = Item("Confirmed snapshot");
        var order = Order(account, "same", OrderType.FBS, null);
        var confirmed = new OrderMarketplaceItem { Id = Guid.NewGuid(), CatalogItem = snapshot, Quantity = 1 };
        order.MarketplaceItems.Add(confirmed);
        order.MarketplaceItems.Add(new OrderMarketplaceItem
        {
            Id = Guid.NewGuid(), CatalogItem = Item("Other item"), Quantity = 1,
        });
        var attempt = Attempt(account, "attempt", new(2026, 9, 25), order);
        attempt.Sku = null;
        attempt.OfferId = "";
        attempt.OrderMarketplaceItem = confirmed;
        attempt.CatalogItem = snapshot;
        var fee = Journal(account, order, new(2026, 10, 1), attempt.SourceExternalId);
        fee.Sku = null;
        fee.OrderMarketplaceItem = confirmed;
        fee.CatalogItem = snapshot;
        db.AddRange(attempt, fee, Journal(account, order, new(2026, 9, 17)));
        await db.SaveChangesAsync();

        await Service(db).ReconcileWildberriesAsync(account.Id, new MarketplaceSyncRun(), CancellationToken.None);
        db.ChangeTracker.Clear();

        var savedAttempt = await db.MarketplaceReturns.SingleAsync(r => r.Id == attempt.Id);
        var savedFee = await db.MarketplaceAccruals.SingleAsync(r => r.Id == fee.Id);
        Assert.Equal(confirmed.Id, savedAttempt.OrderMarketplaceItemId);
        Assert.Equal(snapshot.Id, savedAttempt.CatalogItemId);
        Assert.Equal(confirmed.Id, savedFee.OrderMarketplaceItemId);
        Assert.Equal(snapshot.Id, savedFee.CatalogItemId);
    }

    [PostgresTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Reconcile_MissingCardUsesOnlyUnambiguousItemOfNewlySelectedOrder(bool multipleItems)
    {
        await using var scope = database.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var account = Account();
        var oldOrder = Order(account, "old", OrderType.FBS, null);
        var oldItem = new OrderMarketplaceItem
        {
            Id = Guid.NewGuid(), CatalogItem = Item("Old snapshot"), Quantity = 1,
        };
        oldOrder.MarketplaceItems.Add(oldItem);
        var newOrder = Order(account, "new", OrderType.FboPosting, null);
        var snapshot = Item("New snapshot");
        var newItem = new OrderMarketplaceItem { Id = Guid.NewGuid(), CatalogItem = snapshot, Quantity = 1 };
        newOrder.MarketplaceItems.Add(newItem);
        if (multipleItems)
            newOrder.MarketplaceItems.Add(new OrderMarketplaceItem
            {
                Id = Guid.NewGuid(), CatalogItem = Item("Another snapshot"), Quantity = 1,
            });
        var attempt = Attempt(account, "attempt", new(2026, 9, 25), oldOrder);
        attempt.Sku = null;
        attempt.OfferId = "";
        attempt.OrderMarketplaceItem = oldItem;
        attempt.CatalogItem = oldItem.CatalogItem;
        var fee = Journal(account, oldOrder, new(2026, 10, 1), attempt.SourceExternalId);
        fee.Sku = null;
        fee.OrderMarketplaceItem = oldItem;
        fee.CatalogItem = oldItem.CatalogItem;
        db.AddRange(attempt, fee, Journal(account, oldOrder, new(2026, 7, 1)),
            Journal(account, newOrder, new(2026, 9, 17)));
        await db.SaveChangesAsync();

        await Service(db).ReconcileWildberriesAsync(account.Id, new MarketplaceSyncRun(), CancellationToken.None);
        db.ChangeTracker.Clear();

        var savedAttempt = await db.MarketplaceReturns.SingleAsync(r => r.Id == attempt.Id);
        var savedFee = await db.MarketplaceAccruals.SingleAsync(r => r.Id == fee.Id);
        Assert.Equal(newOrder.Id, savedAttempt.OrderId);
        Assert.Equal(newOrder.Id, savedFee.OrderId);
        Assert.Equal(multipleItems ? (Guid?)null : newItem.Id, savedAttempt.OrderMarketplaceItemId);
        Assert.Equal(multipleItems ? (Guid?)null : snapshot.Id, savedAttempt.CatalogItemId);
        Assert.Equal(savedAttempt.OrderMarketplaceItemId, savedFee.OrderMarketplaceItemId);
        Assert.Equal(savedAttempt.CatalogItemId, savedFee.CatalogItemId);
    }

    private static MarketplaceReturnSyncService Service(ApplicationDbContext db) =>
        new(db, new SilentRealtime(), Options.Create(new MarketplacesOptions()));

    private static MarketplaceAccount Account() => new()
    {
        Id = Guid.NewGuid(), Type = MarketplaceType.Wildberries, Name = "Returns test",
        ApiKeyProtected = "unused", ApiKeyLast4 = "test",
    };

    private static CatalogItem Item(string name) => new()
    {
        Id = Guid.NewGuid(), Name = name, Article = Guid.NewGuid().ToString("N"),
    };

    private static Order Order(MarketplaceAccount account, string posting, OrderType type, string? label,
        MarketplaceCard? card = null, CatalogItem? snapshot = null)
    {
        var order = new Order
        {
            Id = Guid.NewGuid(), Type = type, IsExternal = true, Status = OrderStatus.Shipped,
            MarketplaceOrder = new MarketplaceOrder
            {
                MarketplaceAccount = account, PostingNumber = posting, Rid = "customer-" + posting,
                LabelBarcode = label, Status = MarketplaceOrderStatus.Cancelled,
                InProcessAt = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            },
        };
        if (card is not null)
            order.MarketplaceItems.Add(new OrderMarketplaceItem
            {
                Id = Guid.NewGuid(), MarketplaceCard = card, CatalogItem = snapshot, Quantity = 1,
            });
        return order;
    }

    private static MarketplaceReturn Attempt(MarketplaceAccount account, string rid, DateOnly requested, Order old) => new()
    {
        Id = Guid.NewGuid(), MarketplaceAccount = account,
        ExternalId = WildberriesReturnLinker.SellerAttemptPrefix + rid, SourceExternalId = rid,
        ExemplarId = "unit", RequestedOn = requested, PostingNumber = old.MarketplaceOrder!.PostingNumber,
        Order = old, OfferId = "offer", Sku = "sku", Quantity = 1,
        Kind = MarketplaceReturnKind.FullRefusal, Scheme = MarketplaceReturnScheme.Fbs,
    };

    private static MarketplaceAccrual Journal(MarketplaceAccount account, Order order, DateOnly day,
        string? sellerRid = null) => new()
    {
        Id = Guid.NewGuid(), MarketplaceAccount = account, ExternalId = Guid.NewGuid().ToString("N"),
        Date = day, Source = MarketplaceAccrualSource.AccrualJournal,
        Category = MarketplaceAccrualCategory.ReturnLogistics, Scope = MarketplaceAccrualScope.Item,
        RawTypeId = "Логистика",
        ExemplarId = "unit", Order = order, Rid = sellerRid ?? order.MarketplaceOrder!.Rid,
        UnitNumber = sellerRid is null ? order.MarketplaceOrder!.PostingNumber : null,
        Amount = -100, CurrencyCode = "RUB", Sku = "sku",
    };

    private sealed class SilentRealtime : IRealtimeNotifier
    {
        public ValueTask PublishAsync(RealtimeAddress address, RealtimeEvent evt, CancellationToken ct = default) =>
            ValueTask.CompletedTask;
    }
}
