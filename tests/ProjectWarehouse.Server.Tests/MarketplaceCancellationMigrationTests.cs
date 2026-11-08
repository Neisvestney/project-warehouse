using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.DependencyInjection;
using ProjectWarehouse.Server.Data;
using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Migrations;

namespace ProjectWarehouse.Server.Tests;

public class MarketplaceCancellationMigrationTests(TokenDatabase database) : IClassFixture<TokenDatabase>
{
    [PostgresFact]
    public async Task Migration_UpdatesOnlyConfirmedMissingWildberriesFboInitiators()
    {
        await using var scope = database.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var wb = Account(MarketplaceType.Wildberries);
        var ozon = Account(MarketplaceType.Ozon);
        var regular = Posting(wb);
        var reconstructed = Posting(wb);
        reconstructed.IsReconstructed = true;
        reconstructed.RawSubstatus = "FBW, (МГТ, короба)";
        var unrecognized = Posting(wb);
        unrecognized.RawCancellationType = "future-initiator";
        var emptyRaw = Posting(wb);
        emptyRaw.RawCancellationType = "";
        var fbs = Posting(wb);
        fbs.Order.Type = OrderType.FBS;
        var otherProvider = Posting(ozon);
        var delivered = Posting(wb);
        delivered.Status = MarketplaceOrderStatus.Delivered;
        var recognized = Posting(wb);
        recognized.CancellationType = MarketplaceCancellationType.Customer;
        var absentCancellation = Posting(wb);
        absentCancellation.CancellationType = null;
        var rawStatus = Posting(wb);
        rawStatus.RawStatus = "future-status";
        var afterAssembly = Posting(wb);
        afterAssembly.CancelledAfterAssembly = true;
        var internalOrder = Posting(wb);
        internalOrder.Order.IsExternal = false;
        MarketplaceOrder[] unchanged = [unrecognized, emptyRaw, fbs, otherProvider, delivered, recognized,
            absentCancellation, rawStatus, afterAssembly, internalOrder];
        MarketplaceOrder[] postings = [regular, reconstructed, .. unchanged];
        db.AddRange(postings);
        await db.SaveChangesAsync();

        var migration = new SeparateMarketplaceCancellationNoData();
        foreach (var operation in migration.UpOperations.OfType<SqlOperation>())
            await db.Database.ExecuteSqlRawAsync(operation.Sql);
        db.ChangeTracker.Clear();

        var ids = postings.Select(p => p.OrderId).ToArray();
        var saved = await db.MarketplaceOrders.Where(p => ids.Contains(p.OrderId)).ToDictionaryAsync(p => p.OrderId);
        Assert.Equal(MarketplaceCancellationType.NoData, saved[regular.OrderId].CancellationType);
        Assert.Equal(MarketplaceCancellationType.NoData, saved[reconstructed.OrderId].CancellationType);
        foreach (var posting in unchanged)
            Assert.Equal(posting.CancellationType, saved[posting.OrderId].CancellationType);
        Assert.Equal("future-initiator", saved[unrecognized.OrderId].RawCancellationType);
        Assert.True(saved[reconstructed.OrderId].IsReconstructed);

        foreach (var operation in migration.DownOperations.OfType<SqlOperation>())
            await db.Database.ExecuteSqlRawAsync(operation.Sql);
        db.ChangeTracker.Clear();
        var reverted = await db.MarketplaceOrders.Where(p => ids.Contains(p.OrderId)).ToDictionaryAsync(p => p.OrderId);
        foreach (var posting in postings)
            Assert.Equal(posting.CancellationType, reverted[posting.OrderId].CancellationType);
    }

    private static MarketplaceAccount Account(MarketplaceType type) => new()
    {
        Id = Guid.NewGuid(), Type = type, Name = "Cancellation migration test",
        ApiKeyProtected = "unused", ApiKeyLast4 = "test",
    };

    private static MarketplaceOrder Posting(MarketplaceAccount account) => new()
    {
        Order = new Order { Id = Guid.NewGuid(), Type = OrderType.FboPosting, IsExternal = true },
        MarketplaceAccount = account, PostingNumber = Guid.NewGuid().ToString(),
        Status = MarketplaceOrderStatus.Cancelled, CancellationType = MarketplaceCancellationType.Unknown,
    };
}
