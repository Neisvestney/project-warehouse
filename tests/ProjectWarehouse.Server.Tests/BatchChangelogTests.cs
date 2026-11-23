using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ProjectWarehouse.Server.Data;
using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Infrastructure.ChangeLog;
using ProjectWarehouse.Server.Infrastructure.Realtime;
using ProjectWarehouse.Server.Models.Catalog;
using ProjectWarehouse.Server.Models.Analytics;
using ProjectWarehouse.Server.Models.Roles;

namespace ProjectWarehouse.Server.Tests;

public class BatchChangelogTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TypedServicePreservesComparisonOverrides(bool batch)
    {
        using var db = new RecordingDb();
        IChangeLogService<AnalyticsSettingsDto> service = new AnalyticsSettingsDtoChangelogService(
            CreateService(db, new RecordingNotifier()));
        var id = Guid.NewGuid();
        var before = new AnalyticsSettingsDto { Id = id, Version = 1 };
        var after = new AnalyticsSettingsDto { Id = id, Version = 2, UpdatedAt = DateTime.UtcNow, UpdatedByName = "Admin" };

        if (batch)
            await service.CompareAndSaveBatchToChangelog([new(before, after)]);
        else
            await service.CompareAndSaveToChangelog(before, after);

        Assert.Equal(0, db.Saves);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RolesHistoryUsesSharedEntityKey(bool batch)
    {
        using var db = new RecordingDb();
        IChangeLogService<RolesListDto> service = new RolesListDtoChangelogService(
            CreateService(db, new RecordingNotifier()));
        var after = new RolesListDto { Id = Guid.NewGuid() };

        if (batch)
            await service.CompareAndSaveBatchToChangelog([new(null, after)]);
        else
            await service.CompareAndSaveToChangelog(null, after);

        var entry = Assert.Single(db.Saved);
        Assert.Equal(AppEntityType.Roles, entry.EntityType);
        Assert.Equal(Guid.Empty, entry.EntityId);
    }

    [Fact]
    public async Task BatchSavesOnceAndPublishesOnlyAfterSave()
    {
        using var db = new RecordingDb();
        var notifier = new RecordingNotifier(() => Assert.Equal(1, db.Saves));
        var service = CreateService(db, notifier);
        var changes = Enumerable.Range(0, 10).Select(_ => Change(Guid.NewGuid())).ToList();

        await service.CompareAndSaveBatchToChangelog(changes);

        Assert.Equal(1, db.Saves);
        Assert.Equal(10, db.Saved.Count);
        Assert.Equal(10, notifier.Events);
        Assert.All(db.Saved, entry => Assert.Equal("updated", entry.Action));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EmptyAndUnchangedBatchesDoNotSaveOrPublish(bool unchanged)
    {
        using var db = new RecordingDb();
        var notifier = new RecordingNotifier();
        var service = CreateService(db, notifier);
        var value = new { Name = "same" };
        ChangeLogComparison[] changes = unchanged
            ? [new(AppEntityType.Order, Guid.NewGuid(), value, value)]
            : [];

        await service.CompareAndSaveBatchToChangelog(changes);

        Assert.Equal(0, db.Saves);
        Assert.Equal(0, notifier.Events);
    }

    [Fact]
    public async Task FailedSaveDoesNotPublish()
    {
        using var db = new RecordingDb { Fail = true };
        var notifier = new RecordingNotifier();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateService(db, notifier).CompareAndSaveBatchToChangelog([Change(Guid.NewGuid())]));

        Assert.Empty(db.Saved);
        Assert.Equal(0, notifier.Events);
    }

    [Fact]
    public async Task BatchPreservesSingleEntryContents()
    {
        using var singleDb = new RecordingDb();
        using var batchDb = new RecordingDb();
        var change = Change(Guid.NewGuid());
        await CreateService(singleDb, new RecordingNotifier()).CompareAndSaveToChangelog(
            change.EntityType, change.EntityId, change.Before, change.After,
            action: change.Action, actionData: change.ActionData);
        await CreateService(batchDb, new RecordingNotifier()).CompareAndSaveBatchToChangelog([change]);

        var single = Assert.Single(singleDb.Saved);
        var batch = Assert.Single(batchDb.Saved);
        Assert.Equal(single.Snapshot, batch.Snapshot);
        Assert.Equal(single.Context, batch.Context);
        Assert.Equal(single.ActionData, batch.ActionData);
        Assert.Equal(single.ChangeLogEntryType, batch.ChangeLogEntryType);
        Assert.Equal(single.EntityId, batch.EntityId);
        Assert.Equal(single.Diffs.Select(d => (d.Path, d.From, d.To)), batch.Diffs.Select(d => (d.Path, d.From, d.To)));
    }

    [Fact]
    public async Task CatalogBatchIncludesChildTagChangeInGroupHistory()
    {
        using var db = new RecordingDb();
        var service = new CatalogItemDtoChangelogService(CreateService(db, new RecordingNotifier()));
        var childId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var beforeChild = new CatalogItemDto { Id = childId };
        var afterChild = new CatalogItemDto
        {
            Id = childId,
            Tags = [new CatalogItemTagDto { Id = Guid.NewGuid(), Name = "Tag" }],
        };
        await service.CompareAndSaveBatchToChangelog([
            new(new CatalogItemDto { Id = groupId, Children = [beforeChild] },
                new CatalogItemDto { Id = groupId, Children = [afterChild] }),
            new(beforeChild, afterChild),
        ]);

        Assert.Equal(1, db.Saves);
        Assert.Equal(2, db.Saved.Count);
        Assert.Contains(db.Saved.Single(e => e.EntityId == groupId).Diffs, d => d.Path.Contains("Children"));
    }

    [Fact]
    public async Task CancelledBatchDoesNotSaveOrPublish()
    {
        using var db = new RecordingDb();
        var notifier = new RecordingNotifier();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CreateService(db, notifier)
            .CompareAndSaveBatchToChangelog([Change(Guid.NewGuid())], new CancellationToken(true)));
        Assert.Equal(0, db.Saves);
        Assert.Equal(0, notifier.Events);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TransactionOutboxFlushesOnlyOnCommit(bool commit)
    {
        using var db = new RecordingDb();
        var http = new DefaultHttpContext();
        var accessor = new HttpContextAccessor { HttpContext = http };
        var notifier = new RealtimeNotifier(null!, null!, accessor, NullLogger<RealtimeNotifier>.Instance);
        var service = new AppChangeLogService(db, accessor, notifier, Options.Create(new JsonOptions()),
            NullLogger<AppChangeLogService>.Instance);
        var outbox = RealtimeOutbox.Open(http);
        await service.CompareAndSaveBatchToChangelog([Change(Guid.NewGuid()), Change(Guid.NewGuid())]);
        Assert.Same(outbox, RealtimeOutbox.Current(http));
        var delivered = new RecordingNotifier();

        if (!commit) outbox.Discard(http);
        await outbox.FlushAsync(http, delivered, CancellationToken.None);

        Assert.Null(RealtimeOutbox.Current(http));
        Assert.Equal(commit ? 2 : 0, delivered.Events);
    }

    private static ChangeLogComparison Change(Guid id) => new(AppEntityType.Order, id,
        new { Name = "before" }, new { Name = "after" }, Action: "updated", ActionData: new { Source = "bulk" });

    private static AppChangeLogService CreateService(RecordingDb db, IRealtimeNotifier notifier) => new(
        db, new HttpContextAccessor(), notifier, Options.Create(new JsonOptions()),
        NullLogger<AppChangeLogService>.Instance);

    private sealed class RecordingDb() : ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseNpgsql("Host=localhost;Database=unused").Options)
    {
        public int Saves { get; private set; }
        public bool Fail { get; init; }
        public List<ChangeLogEntry> Saved { get; } = [];

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Saves++;
            if (Fail) throw new InvalidOperationException("Save failed.");
            Saved.AddRange(ChangeTracker.Entries<ChangeLogEntry>().Select(e => e.Entity));
            return Task.FromResult(Saved.Count);
        }
    }

    private sealed class RecordingNotifier(Action? beforePublish = null) : IRealtimeNotifier
    {
        public int Events { get; private set; }

        public ValueTask PublishAsync(RealtimeAddress address, RealtimeEvent evt, CancellationToken ct = default)
        {
            beforePublish?.Invoke();
            Events++;
            return ValueTask.CompletedTask;
        }
    }
}
