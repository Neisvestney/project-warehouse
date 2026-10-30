using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Neisvestney.Marketplaces.Wildberries;
using ProjectWarehouse.Server.Data;
using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Infrastructure.Marketplaces;
using ProjectWarehouse.Server.Infrastructure.Realtime;
using ProjectWarehouse.Server.Integrations.Abstractions;
using ProjectWarehouse.Server.Integrations.Ozon;
using ProjectWarehouse.Server.Integrations.Wildberries;
using ProjectWarehouse.Server.Services;
using Options = Microsoft.Extensions.Options.Options;

namespace ProjectWarehouse.Server.Tests;

public class WildberriesAccrualTests(TokenDatabase database) : IClassFixture<TokenDatabase>
{
    [Theory]
    [InlineData(false, "3400", 1, "2216", "1931.86", "108.14", "3400", "1184", "-1360")]
    [InlineData(true, "3400", 1, "2216", "1931.86", "108.14", "3400", "1184", "-1360")]
    [InlineData(false, "5040", 1, "2883", "2959.08", "115.32", "5040", "2157", "-1965.60")]
    [InlineData(false, "3400", 2, "4432", "3863.72", "216.28", "6800", "2368", "-2720")]
    [InlineData(false, "3400", 1, "3400", "1931.86", "108.14", "3400", "0", "-1360")]
    [InlineData(false, null, 1, "2216", "1931.86", "108.14", "2216", null, "-176")]
    [InlineData(false, "3400", 0, "2216", "1931.86", "108.14", "2216", null, "-176")]
    public async Task GetAccruals_NormalizesSellerPriceAndPreservesPayout(bool reversal, string? price,
        int quantity, string retail, string payout, string acquiring, string sale, string? discount, string commission)
    {
        var row = Row(reversal);
        row.RetailPriceWithDisc = price!;
        row.Quantity = quantity;
        row.RetailAmount = retail;
        row.ForPay = payout;
        row.AcquiringFee = acquiring;
        using var handler = new ReportHandler(row);
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://wildberries.test/") };
        var client = Client(http);
        var results = new List<ExternalAccrual>();
        await foreach (var page in client.GetAccrualsAsync(new(2026, 1, 5), new(2026, 1, 6), CancellationToken.None))
            results.AddRange(page);

        var lines = Assert.Single(results).Lines;
        var sign = reversal ? -1m : 1m;
        var saleLine = Assert.Single(lines, l => l.Category == MarketplaceAccrualCategory.Sale);
        Assert.Equal(sign * Money(sale), saleLine.Amount);
        Assert.Equal(discount is null ? (decimal?)null : sign * Money(discount), saleLine.PlatformDiscountAmount);
        Assert.Equal(sign * Money(commission), Assert.Single(lines, l => l.Category == MarketplaceAccrualCategory.Commission).Amount);
        Assert.All(lines.Where(l => l.Category != MarketplaceAccrualCategory.Sale), l => Assert.Null(l.PlatformDiscountAmount));
        Assert.Equal(sign * Money(payout), lines.Sum(l => l.Amount));
    }

    [PostgresTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Backfill_ReplacesOldAmountsPersistsDiscountAndIsIdempotent(bool reversal)
    {
        await using var scope = database.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var account = new MarketplaceAccount
        {
            Id = Guid.NewGuid(), Type = MarketplaceType.Wildberries, Name = "Accrual test",
            ApiKeyProtected = "unused", ApiKeyLast4 = "test",
        };
        var sign = reversal ? -1m : 1m;
        var categories = new[] { MarketplaceAccrualCategory.Sale, MarketplaceAccrualCategory.Acquiring,
            MarketplaceAccrualCategory.Commission, MarketplaceAccrualCategory.Other };
        var amounts = new[] { 2216m, -108.14m, -176m, 0m };
        for (var i = 0; i < categories.Length; i++)
            db.MarketplaceAccruals.Add(new MarketplaceAccrual
            {
                MarketplaceAccount = account, ExternalId = "1", LineNo = i, Date = new(2026, 1, 5),
                Source = MarketplaceAccrualSource.AccrualJournal, Scope = MarketplaceAccrualScope.Account,
                Category = categories[i], RawTypeId = reversal ? "Возврат" : "Продажа",
                Amount = sign * amounts[i], CurrencyCode = "RUB",
            });
        await db.SaveChangesAsync();

        using var handler = new ReportHandler(Row(reversal));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://wildberries.test/") };
        var provider = new WildberriesMarketplaceProvider(Client(http), new MarketplaceRequestContext(),
            NullLogger<WildberriesMarketplaceProvider>.Instance);
        var options = Options.Create(new MarketplacesOptions());
        var realtime = new SilentRealtime();
        var service = new MarketplaceAccrualSyncService(db, realtime, options,
            new MarketplaceReturnSyncService(db, realtime, options));
        var credentials = new MarketplaceCredentials(null, "unused");
        var from = new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc);

        async Task<MarketplaceSyncRun> Import()
        {
            var run = new MarketplaceSyncRun();
            await service.SyncAccrualsBackfillAsync(provider, credentials, account, run, from, from.AddDays(1),
                CancellationToken.None);
            return run;
        }

        var first = await Import();
        Assert.Equal(1, first.AccrualsUpdated);
        Assert.Equal(0, first.AccrualsCreated);
        db.ChangeTracker.Clear();
        var saved = await db.MarketplaceAccruals.Where(a => a.MarketplaceAccountId == account.Id).OrderBy(a => a.LineNo).ToListAsync();
        Assert.Equal(3, saved.Count);
        Assert.Equal(sign * 1931.86m, saved.Sum(a => a.Amount));
        Assert.Equal(sign * 3400m, saved[0].Amount);
        Assert.Equal(sign * 1184m, saved[0].PlatformDiscountAmount);
        Assert.Equal(sign * -1360m, saved[2].Amount);
        var ids = saved.Select(a => a.Id).ToArray();

        saved[0].PlatformDiscountAmount = null;
        await db.SaveChangesAsync();
        var discountOnly = await Import();
        Assert.Equal(1, discountOnly.AccrualsUpdated);
        db.ChangeTracker.Clear();
        var unchanged = await Import();
        Assert.Equal(0, unchanged.AccrualsUpdated);
        Assert.Equal(0, unchanged.AccrualsCreated);
        var final = await db.MarketplaceAccruals.Where(a => a.MarketplaceAccountId == account.Id).OrderBy(a => a.LineNo).ToListAsync();
        Assert.Equal(ids, final.Select(a => a.Id).ToArray());
        Assert.Equal(sign * 1184m, final[0].PlatformDiscountAmount);
    }

    private static decimal Money(string value) => decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture);

    private static SalesReportsDetailedRes Row(bool reversal) => new()
    {
        RrdId = 1, RrDate = "2026-01-05", DocTypeName = reversal ? "Возврат" : "Продажа",
        SellerOperName = reversal ? "Возврат" : "Продажа", RetailPriceWithDisc = "3400", Quantity = 1,
        RetailAmount = "2216", ForPay = "1931.86", AcquiringFee = "108.14", Currency = "RUB",
    };

    private static WildberriesClient Client(HttpClient http) => new(new WildberriesCommonClient(http),
        new WildberriesContentClient(http), new WildberriesMarketplaceClient(http),
        new WildberriesSellerAnalyticsClient(http), new WildberriesFinanceClient(http),
        new WildberriesStatisticsClient(http), Options.Create(new MarketplacesOptions()),
        NullLogger<WildberriesClient>.Instance);

    private sealed class ReportHandler(SalesReportsDetailedRes row) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var body = request.RequestUri!.AbsolutePath switch
            {
                "/api/finance/v1/sales-reports/detailed" => JsonSerializer.Serialize(new[] { row }),
                "/content/v2/get/cards/list" or "/content/v2/get/cards/trash" => "{\"cards\":[]}",
                _ => throw new InvalidOperationException("Unexpected marketplace request: " + request.RequestUri),
            };
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            });
        }
    }

    private sealed class SilentRealtime : IRealtimeNotifier
    {
        public ValueTask PublishAsync(RealtimeAddress address, RealtimeEvent evt, CancellationToken ct = default) =>
            ValueTask.CompletedTask;
    }
}
