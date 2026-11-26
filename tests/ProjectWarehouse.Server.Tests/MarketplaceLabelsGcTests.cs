using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ProjectWarehouse.Server.Data;
using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Infrastructure.Marketplaces;
using Quartz;

namespace ProjectWarehouse.Server.Tests;

public class MarketplaceLabelsGcTests(TokenDatabase database) : IClassFixture<TokenDatabase>
{
    [PostgresTheory]
    [InlineData(MarketplaceOrderStatus.Delivered, -60, -40, null, -90, true)]
    [InlineData(MarketplaceOrderStatus.Delivered, -60, -2, null, -90, false)]
    [InlineData(MarketplaceOrderStatus.Delivered, -2, -60, null, -90, false)]
    [InlineData(MarketplaceOrderStatus.Cancelled, -60, null, -2, -90, false)]
    [InlineData(MarketplaceOrderStatus.Cancelled, -2, null, -60, -90, false)]
    [InlineData(MarketplaceOrderStatus.Cancelled, null, null, -40, -2, true)]
    [InlineData(MarketplaceOrderStatus.Delivered, null, -40, null, null, true)]
    [InlineData(MarketplaceOrderStatus.Delivered, null, null, null, -40, true)]
    [InlineData(MarketplaceOrderStatus.Cancelled, null, null, null, -40, true)]
    [InlineData(MarketplaceOrderStatus.Cancelled, null, null, null, -2, false)]
    [InlineData(MarketplaceOrderStatus.Cancelled, null, null, null, null, false)]
    [InlineData(MarketplaceOrderStatus.AwaitingDeliver, -60, -40, null, -90, false)]
    [InlineData(MarketplaceOrderStatus.Delivering, -60, null, null, -90, false)]
    [InlineData(MarketplaceOrderStatus.AwaitingPackaging, -60, null, null, -90, false)]
    [InlineData(MarketplaceOrderStatus.Arbitration, -60, null, null, -90, false)]
    [InlineData(MarketplaceOrderStatus.Unknown, -60, null, null, -90, false)]
    [InlineData(MarketplaceOrderStatus.Split, -60, null, null, -90, false)]
    [InlineData(MarketplaceOrderStatus.Delivered, null, -40, null, -90, false, OrderStatus.Draft, false)]
    [InlineData(MarketplaceOrderStatus.Delivered, null, -40, null, -90, false, OrderStatus.Confirmed, false)]
    [InlineData(MarketplaceOrderStatus.Delivered, null, -40, null, -90, false, OrderStatus.Assembly, false)]
    [InlineData(MarketplaceOrderStatus.Delivered, -60, -40, null, -90, false, OrderStatus.Assembled, false)]
    [InlineData(MarketplaceOrderStatus.Delivered, null, -40, null, -90, false, OrderStatus.Canceled, false)]
    [InlineData(MarketplaceOrderStatus.Cancelled, null, null, -40, -90, false, OrderStatus.Draft, false)]
    [InlineData(MarketplaceOrderStatus.Cancelled, null, null, -40, -90, false, OrderStatus.Confirmed, false)]
    [InlineData(MarketplaceOrderStatus.Cancelled, null, null, -40, -90, false, OrderStatus.Assembly, false)]
    [InlineData(MarketplaceOrderStatus.Cancelled, -60, null, -40, -90, false, OrderStatus.Assembled, false)]
    [InlineData(MarketplaceOrderStatus.Cancelled, null, null, -40, -90, false, OrderStatus.Canceled, false)]
    [InlineData(MarketplaceOrderStatus.Delivered, -60, -40, null, -90, true, OrderStatus.Shipped, false)]
    [InlineData(MarketplaceOrderStatus.Cancelled, -60, null, -40, -90, true, OrderStatus.Shipped, false)]
    [InlineData(MarketplaceOrderStatus.Delivered, -2, -40, null, -90, false, OrderStatus.Shipped, false)]
    [InlineData(MarketplaceOrderStatus.Cancelled, null, null, -40, -90, true, OrderStatus.Canceled, true)]
    public async Task Execute_ReleasesOnlyExpiredTerminalLabels(MarketplaceOrderStatus status,
        int? shippedDays, int? deliveredDays, int? cancelledDays, int? fetchedDays, bool expires,
        OrderStatus orderStatus = OrderStatus.Shipped, bool isExternal = true)
    {
        await using var scope = database.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var now = DateTime.UtcNow.Date;
        DateTime? At(int? days) => days is { } value ? now.AddDays(value) : null;
        var file = new DataFile
        {
            Id = Guid.NewGuid(), StorageKey = Guid.NewGuid().ToString("N"),
            OriginalFileName = "label.pdf", ContentType = "application/pdf", CreatedAt = now.AddDays(-90),
        };
        var parcel = new MarketplaceParcel
        {
            Id = Guid.NewGuid(), Index = 1, LabelFile = file,
            LabelKind = LabelKind.OzonPostingLabel, LabelFetchedAt = At(fetchedDays),
            MarketplaceOrder = new MarketplaceOrder
            {
                Order = new Order
                {
                    Id = Guid.NewGuid(), Type = OrderType.FBS, Status = orderStatus,
                    IsExternal = isExternal, ShippedAt = At(shippedDays),
                },
                MarketplaceAccount = new MarketplaceAccount
                {
                    Id = Guid.NewGuid(), Type = MarketplaceType.Ozon, Name = "Label GC test",
                    ApiKeyProtected = "unused", ApiKeyLast4 = "test",
                },
                PostingNumber = Guid.NewGuid().ToString(), Status = status,
                DeliveredAt = At(deliveredDays), CancelledAt = At(cancelledDays),
            },
        };
        db.Add(parcel);
        await db.SaveChangesAsync();
        var fetchedAt = parcel.LabelFetchedAt;
        var job = new MarketplaceLabelsGcJob(db,
            Options.Create(new MarketplacesOptions { Labels = new LabelsOptions { CacheTtlDays = 30 } }),
            NullLogger<MarketplaceLabelsGcJob>.Instance);

        await job.Execute(DispatchProxy.Create<IJobExecutionContext, JobContext>());
        await db.Entry(parcel).ReloadAsync();

        Assert.Equal(expires ? (Guid?)null : file.Id, parcel.LabelFileId);
        Assert.Equal(expires ? (LabelKind?)null : LabelKind.OzonPostingLabel, parcel.LabelKind);
        Assert.Equal(expires ? null : fetchedAt, parcel.LabelFetchedAt);
        Assert.True(await db.DataFiles.AnyAsync(f => f.Id == file.Id));
    }

    public class JobContext : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
            targetMethod?.Name == "get_CancellationToken"
                ? CancellationToken.None
                : throw new NotSupportedException(targetMethod?.Name);
    }
}
