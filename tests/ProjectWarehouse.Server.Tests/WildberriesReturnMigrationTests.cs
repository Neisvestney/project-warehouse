using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.DependencyInjection;
using ProjectWarehouse.Server.Data;
using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Migrations;

namespace ProjectWarehouse.Server.Tests;

public class WildberriesReturnMigrationTests(TokenDatabase database) : IClassFixture<TokenDatabase>
{
    [PostgresFact]
    public async Task Migration_SeparatesLegacySellerDataAndRewindsHistoryWithoutChangingOtherMarketplaces()
    {
        await using var scope = database.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var wb = Account(MarketplaceType.Wildberries);
        var ozon = Account(MarketplaceType.Ozon);
        var order = new Order { Id = Guid.NewGuid(), Type = OrderType.FBS, IsExternal = true };
        var buyer = Return(wb, "unit", "latest");
        buyer.Order = order;
        buyer.PostingNumber = "original";
        buyer.SaleReversedOn = new DateOnly(2026, 7, 12);
        buyer.Kind = MarketplaceReturnKind.CustomerReturn;
        buyer.Price = 3556;
        var standalone = Return(wb, "other-unit", "other-attempt");
        var untouched = Return(ozon, "ozon-return", "ozon-source");
        untouched.SaleReversedOn = buyer.SaleReversedOn;
        var fee = new MarketplaceAccrual
        {
            Id = Guid.NewGuid(), MarketplaceAccount = wb, ExternalId = "seller-fee", Rid = "latest",
            Date = new DateOnly(2026, 10, 6), Order = order,
        };
        db.AddRange(buyer, standalone, untouched, fee);
        await db.SaveChangesAsync();

        foreach (var operation in new PreserveWildberriesReturnAttempts().UpOperations.OfType<SqlOperation>())
            await db.Database.ExecuteSqlRawAsync(operation.Sql);
        db.ChangeTracker.Clear();

        var savedBuyer = await db.MarketplaceReturns.SingleAsync(r => r.Id == buyer.Id);
        Assert.Null(savedBuyer.SourceExternalId);
        Assert.Null(savedBuyer.RawStatus);
        Assert.Null(savedBuyer.FinalAt);
        Assert.Equal(new DateTime(2026, 7, 11, 21, 0, 0, DateTimeKind.Utc), savedBuyer.ReturnedAt);
        Assert.Equal(savedBuyer.ReturnedAt, savedBuyer.StatusChangedAt);
        Assert.Equal(order.Id, savedBuyer.OrderId);
        Assert.Equal(3556m, savedBuyer.Price);
        Assert.True(savedBuyer.IsCountedAsReturn);
        var attempt = await db.MarketplaceReturns.SingleAsync(r => r.MarketplaceAccountId == wb.Id
            && r.ExternalId == "wb-seller-return:latest");
        Assert.Null(attempt.SaleReversedOn);
        Assert.Null(attempt.OrderId);
        Assert.Null(attempt.Price);
        Assert.Equal("latest", attempt.SourceExternalId);
        Assert.Equal("Expired", attempt.RawStatus);
        Assert.False(attempt.IsCountedAsReturn);
        Assert.Equal("wb-seller-return:other-attempt",
            (await db.MarketplaceReturns.SingleAsync(r => r.Id == standalone.Id)).ExternalId);
        Assert.Equal(1, await db.MarketplaceReturns.Where(r => r.MarketplaceAccountId == wb.Id)
            .Where(r => r.IsCountedAsReturn).SumAsync(r => r.Quantity));
        Assert.Null((await db.MarketplaceAccruals.SingleAsync(r => r.Id == fee.Id)).OrderId);
        Assert.True((await db.MarketplaceAccounts.SingleAsync(a => a.Id == wb.Id)).ReturnsSyncedAt
                    < new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc));
        var savedOzon = await db.MarketplaceReturns.SingleAsync(r => r.Id == untouched.Id);
        Assert.Equal("ozon-source", savedOzon.SourceExternalId);
        Assert.Equal(untouched.ReturnedAt, savedOzon.ReturnedAt);
        Assert.Equal(ozon.ReturnsSyncedAt,
            (await db.MarketplaceAccounts.SingleAsync(a => a.Id == ozon.Id)).ReturnsSyncedAt);
    }

    private static MarketplaceAccount Account(MarketplaceType type) => new()
    {
        Id = Guid.NewGuid(), Type = type, Name = "Migration test", ApiKeyProtected = "unused", ApiKeyLast4 = "test",
        ReturnsSyncedAt = new DateTime(2026, 10, 30, 0, 0, 0, DateTimeKind.Utc),
    };

    private static MarketplaceReturn Return(MarketplaceAccount account, string externalId, string source) => new()
    {
        Id = Guid.NewGuid(), MarketplaceAccount = account, ExternalId = externalId, SourceExternalId = source,
        ExemplarId = externalId, PostingNumber = "", OfferId = "offer", Quantity = 1,
        Kind = MarketplaceReturnKind.FullRefusal, RawStatus = "Expired",
        ReturnedAt = new DateTime(2026, 9, 28, 0, 0, 0, DateTimeKind.Utc),
    };
}
