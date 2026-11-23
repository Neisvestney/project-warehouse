using System.Net;
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

    private static MarketplaceAccount Account(long campaignId) => new()
    {
        Id = Guid.NewGuid(), Type = MarketplaceType.YandexMarket, Name = "Yandex Market test",
        ApiKeyProtected = "unused", ApiKeyLast4 = "test", ExternalBusinessId = 10,
        ExternalCampaignId = campaignId,
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
