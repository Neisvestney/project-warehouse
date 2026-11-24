using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Neisvestney.Marketplaces.YandexMarket;
using Npgsql;
using ProjectWarehouse.Server.Data;
using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Infrastructure;
using ProjectWarehouse.Server.Infrastructure.Marketplaces;
using ProjectWarehouse.Server.Infrastructure.Realtime;
using ProjectWarehouse.Server.Integrations.Abstractions;
using ProjectWarehouse.Server.Integrations.Ozon;
using ProjectWarehouse.Server.Integrations.YandexMarket;
using ProjectWarehouse.Server.Services;
using Options = Microsoft.Extensions.Options.Options;

namespace ProjectWarehouse.Server.Tests;

public class YandexMarketSyncTests(TokenDatabase database) : IClassFixture<TokenDatabase>
{
    [PostgresFact]
    public async Task CampaignIdentity_HasDatabaseUniquenessAcrossBusinesses()
    {
        await using var scope = database.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.MarketplaceAccounts.Add(Account(101));
        await db.SaveChangesAsync();
        var duplicate = Account(101);
        duplicate.ExternalBusinessId = 999;
        db.MarketplaceAccounts.Add(duplicate);
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Equal(PostgresErrorCodes.UniqueViolation, Assert.IsType<PostgresException>(error.InnerException).SqlState);
    }

    [PostgresFact]
    public async Task AccrualQuotaRefusal_PersistsPauseAndPreservesWatermarksAndSavedGroups()
    {
        await using var scope = database.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var account = Account(102);
        account.AccrualsSyncedAt = DateTime.UtcNow.AddDays(-2);
        account.AccrualsFullPassAt = DateTime.UtcNow.AddDays(-2);
        var mark = account.AccrualsSyncedAt;
        var fullMark = account.AccrualsFullPassAt;
        db.MarketplaceAccounts.Add(account);
        db.MarketplaceAccruals.Add(new()
        {
            MarketplaceAccount = account, ExternalId = "account:2026-01-01", LineNo = 0,
            Date = new(2026, 1, 1), Scope = MarketplaceAccrualScope.Account,
            Category = MarketplaceAccrualCategory.Commission, Amount = -15, CurrencyCode = "RUB",
        });
        await db.SaveChangesAsync();
        using var handler = new RefusedReport();
        using var http = new HttpClient(handler) {BaseAddress = new("https://api.partner.market.yandex.ru")};
        var context = new MarketplaceRequestContext();
        var options = Options.Create(new MarketplacesOptions());
        var client = new YandexMarketClient(new YandexMarketApiClient(http), context, null!,
            new(options), options, NullLogger<YandexMarketClient>.Instance);
        var provider = new YandexMarketMarketplaceProvider(client, context,
            NullLogger<YandexMarketMarketplaceProvider>.Instance);
        var realtime = new SilentRealtime();
        var service = new MarketplaceAccrualSyncService(db, realtime, options,
            new MarketplaceReturnSyncService(db, realtime, options), null!, null!);
        var credentials = new MarketplaceCredentials(null, "unused", 10, 102);
        var run = new MarketplaceSyncRun();
        await service.SyncAccrualsAsync(provider, credentials, account, run, default);
        Assert.Equal(ErrorCode.MarketplaceAccrualsPaused, run.Warning?.Code);
        Assert.True(account.AccrualsPausedUntil >= DateTime.UtcNow.AddSeconds(115));
        Assert.Equal(mark, account.AccrualsSyncedAt);
        Assert.Equal(fullMark, account.AccrualsFullPassAt);
        Assert.Equal(-15, await db.MarketplaceAccruals.Where(a => a.MarketplaceAccountId == account.Id).SumAsync(a => a.Amount));
        await db.Entry(account).ReloadAsync();
        Assert.NotNull(account.AccrualsPausedUntil);
        await service.SyncAccrualsAsync(provider, credentials, account, new(), default);
        Assert.Equal(1, handler.Calls);
    }

    [PostgresFact]
    public async Task FbyBackground_ImportsExternalPostingsAndLinksReturnsWithoutWarehouseWork()
    {
        await using var scope = database.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var account = Account(103);
        account.YandexMarketModel = YandexMarketModel.Fby;
        var catalog = new CatalogItem { Id = Guid.NewGuid(), Name = "Product", Article = Guid.NewGuid().ToString("N") };
        db.MarketplaceCards.Add(new() { Id = Guid.NewGuid(), MarketplaceAccount = account, ExternalId = "offer",
            OfferId = "offer", Name = "Product", CatalogItem = catalog });
        await db.SaveChangesAsync();
        using var handler = new FbyOrders();
        var service = OrderService(scope.ServiceProvider, db, handler, out var provider);
        var credentials = new MarketplaceCredentials(null, "unused", 10, 103, YandexMarketModel.Fby);
        await service.SyncOrdersBackgroundAsync(provider, credentials, account, new() { StartedAt = DateTime.UtcNow }, default);
        await service.SyncOrdersBackgroundAsync(provider, credentials, account, new() { StartedAt = DateTime.UtcNow }, default);
        db.ChangeTracker.Clear();

        var orders = await db.Orders.Where(o => o.MarketplaceOrder!.MarketplaceAccountId == account.Id)
            .Include(o => o.MarketplaceOrder).ThenInclude(o => o!.Parcels)
            .Include(o => o.MarketplaceItems).AsSplitQuery().ToListAsync();
        Assert.Equal(2, orders.Count);
        Assert.All(orders, order =>
        {
            Assert.Equal(OrderType.FboPosting, order.Type);
            Assert.True(order.IsExternal);
            Assert.Null(order.WarehouseId);
            Assert.Null(order.PlannedShipmentAt);
            Assert.Single(order.MarketplaceOrder!.Parcels);
            var item = Assert.Single(order.MarketplaceItems);
            Assert.Equal(catalog.Id, item.CatalogItemId);
            Assert.Equal("5", item.ExternalItemId);
            Assert.Equal(2, item.Quantity);
            Assert.Equal(100, item.Price);
        });
        var returnedOrder = orders.Single(o => o.MarketplaceOrder!.PostingNumber == "100");
        var returned = await db.MarketplaceReturns.SingleAsync(r => r.MarketplaceAccountId == account.Id);
        Assert.Equal(MarketplaceReturnScheme.Fbo, returned.Scheme);
        Assert.Equal("FBY", returned.RawScheme);
        Assert.Equal("FULFILMENT_RECEIVED", returned.RawStatus);
        Assert.Equal("Принят на складе Маркета", returned.StatusName);
        Assert.Equal(returnedOrder.Id, returned.OrderId);
        Assert.Equal(returnedOrder.MarketplaceItems.Single().Id, returned.OrderMarketplaceItemId);
        Assert.Equal(catalog.Id, returned.CatalogItemId);
        Assert.Equal(100, returned.Price);
        Assert.Equal(0, await db.AssemblyTasks.CountAsync());
        Assert.Equal(0, await db.AssemblyFulfillments.CountAsync());
        Assert.Equal(0, await db.StockMovements.CountAsync());
        var saved = await db.MarketplaceAccounts.SingleAsync(a => a.Id == account.Id);
        Assert.Equal(YandexMarketModel.Fby, saved.YandexMarketModel);
        Assert.NotNull(saved.FboPostingsSyncedAt);
        Assert.All(handler.OrderRequests, body =>
        {
            using var json = JsonDocument.Parse(body);
            Assert.Equal("FBY", json.RootElement.GetProperty("programTypes")[0].GetString());
        });
    }

    [PostgresFact]
    public async Task FbyBackground_FailedPageKeepsWatermarkAndRetryDoesNotDuplicateSavedOrders()
    {
        await using var scope = database.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var account = Account(104);
        account.YandexMarketModel = YandexMarketModel.Fby;
        account.FboPostingsSyncedAt = DateTime.UtcNow.AddDays(-2);
        db.MarketplaceAccounts.Add(account);
        await db.SaveChangesAsync();
        var originalMark = account.FboPostingsSyncedAt;
        using var handler = new FbyOrders { FailSecondPage = true };
        var service = OrderService(scope.ServiceProvider, db, handler, out var provider);
        var credentials = new MarketplaceCredentials(null, "unused", 10, 104, YandexMarketModel.Fby);
        await Assert.ThrowsAsync<MarketplaceApiException>(() => service.SyncOrdersBackgroundAsync(provider,
            credentials, account, new() { StartedAt = DateTime.UtcNow }, default));
        Assert.Equal(originalMark, account.FboPostingsSyncedAt);
        Assert.Equal(1, await db.MarketplaceOrders.CountAsync(o => o.MarketplaceAccountId == account.Id));
        handler.FailSecondPage = false;
        await service.SyncOrdersBackgroundAsync(provider, credentials, account, new() { StartedAt = DateTime.UtcNow }, default);
        Assert.True(account.FboPostingsSyncedAt > originalMark);
        Assert.Equal(2, await db.MarketplaceOrders.CountAsync(o => o.MarketplaceAccountId == account.Id));
    }

    private static MarketplaceOrderSyncService OrderService(IServiceProvider services, ApplicationDbContext db,
        FbyOrders handler, out YandexMarketMarketplaceProvider provider)
    {
        var http = new HttpClient(handler) { BaseAddress = new("https://api.partner.market.yandex.ru") };
        var context = new MarketplaceRequestContext();
        var options = Options.Create(new MarketplacesOptions { YandexMarket = new() { PageDelayMs = 0 } });
        var client = new YandexMarketClient(new YandexMarketApiClient(http), context, null!, new(options), options,
            NullLogger<YandexMarketClient>.Instance);
        provider = new(client, context, NullLogger<YandexMarketMarketplaceProvider>.Instance);
        var realtime = new SilentRealtime();
        var locks = services.GetRequiredService<IEntityLockService>();
        var returns = new MarketplaceReturnSyncService(db, realtime, options);
        return new(db, locks, realtime, returns, null!, new MarketplaceSupplySyncService(db),
            new ExternalOrderWarehouseService(db, locks), options, NullLogger<MarketplaceOrderSyncService>.Instance);
    }

    private sealed class FbyOrders : HttpMessageHandler
    {
        public bool FailSecondPage { get; set; }
        public List<string> OrderRequests { get; } = [];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            string response;
            if (request.RequestUri!.AbsolutePath.EndsWith("/orders"))
            {
                var body = await request.Content!.ReadAsStringAsync(ct);
                OrderRequests.Add(body);
                using var json = JsonDocument.Parse(body);
                var campaign = json.RootElement.GetProperty("campaignIds")[0].GetInt64();
                var next = request.RequestUri.Query.Contains("pageToken=next");
                if (FailSecondPage && next)
                    return new(HttpStatusCode.BadRequest) { Content = new StringContent("{\"errors\":[{\"code\":\"BAD_REQUEST\"}]}") };
                object Order(long id, string status) => new { orderId = id, campaignId = campaign, programType = "FBY",
                    status, substatus = "STARTED", creationDate = DateTime.UtcNow.AddDays(-1),
                    items = new[] { new { id = 5, offerId = "offer", offerName = "Product", count = 2,
                        prices = new { payment = new { value = 200, currencyId = "RUR" } } } }, delivery = new { dates = new {} } };
                response = FailSecondPage
                    ? JsonSerializer.Serialize(new { orders = new[] { Order(100, "DELIVERED") }, paging = new { nextPageToken = "next" } })
                    : JsonSerializer.Serialize(new { orders = new[] { Order(100, "DELIVERED"), Order(101, "PROCESSING") } });
            }
            else if (request.RequestUri.AbsolutePath.EndsWith("/returns"))
                response = JsonSerializer.Serialize(new { result = new { returns = new[] { new {
                    id = 7, orderId = 100, returnType = "RETURN", refundStatus = "REFUNDED",
                    shipmentStatus = "FULFILMENT_RECEIVED", creationDate = DateTime.UtcNow.AddDays(-1), updateDate = DateTime.UtcNow,
                    items = new[] { new { shopSku = "offer", count = 1 } },
                } } } });
            else throw new InvalidOperationException("Unexpected endpoint: " + request.RequestUri.AbsolutePath);
            return new(HttpStatusCode.OK) { Content = new StringContent(response, Encoding.UTF8, "application/json") };
        }
    }

    private static MarketplaceAccount Account(long campaignId) => new()
    {
        Id = Guid.NewGuid(), Type = MarketplaceType.YandexMarket, Name = "Yandex Market test",
        ApiKeyProtected = "unused", ApiKeyLast4 = "test", ExternalBusinessId = 10,
        ExternalCampaignId = campaignId,
        YandexMarketModel = YandexMarketModel.Fbs,
    };

    private sealed class RefusedReport : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++;
            return Task.FromResult(new HttpResponseMessage((HttpStatusCode)420)
                {Content = new StringContent("{\"errors\":[{\"code\":\"LIMIT\"}]}")});
        }
    }

    private sealed class SilentRealtime : IRealtimeNotifier
    {
        public ValueTask PublishAsync(RealtimeAddress address, RealtimeEvent evt, CancellationToken ct = default) =>
            ValueTask.CompletedTask;
    }
}
