using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using KellermanSoftware.CompareNetObjects;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Infrastructure.Realtime;
using ProjectWarehouse.Server.Models;

namespace ProjectWarehouse.Server.Infrastructure.ChangeLog;

public abstract class AbstractChangeLogService : IChangeLogService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IRealtimeNotifier _realtime;
    private readonly ILogger<AbstractChangeLogService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public AbstractChangeLogService(IHttpContextAccessor httpContextAccessor, IRealtimeNotifier realtime,
        IOptions<JsonOptions> jsonOptions, ILogger<AbstractChangeLogService> logger)
    {
        _httpContextAccessor = httpContextAccessor;
        _realtime = realtime;
        _logger = logger;
        _jsonOptions = jsonOptions.Value.JsonSerializerOptions;
    }

    protected abstract IQueryable<ChangeLogEntry> GetGetChangelogQueryable();
    protected abstract Task AddNewEntry(ChangeLogEntry entry);

    public static CompareLogic GetCompareLogic()
    {
        var logic = new CompareLogic
        {
            Config =
            {
                MaxDifferences = 100,
                IgnoreCollectionOrder = true,
                CollectionMatchingSpec = new Dictionary<Type, IEnumerable<string>>
                {
                    { typeof(IHasIdentity), new[] { "Id" } },
                    { typeof(IHasNullableIdentity), new[] { "Id" } },
                },
                // CompareNETObjects cannot walk IReadOnlyDictionary<string, object> and throws
                // ("Indexer must have a corresponding Count property"). Code and Detail still diff,
                // and Detail already embeds the code, so nothing meaningful is lost.
                MembersToIgnore = { $"{nameof(AppFieldError)}.{nameof(AppFieldError.Args)}" },
            }
        };

        return logic;
    }

    private Guid? GetCurrentUserId()
    {
        return Guid.TryParse(_httpContextAccessor.HttpContext?.User.FindFirstValue(JwtRegisteredClaimNames.Sub), out var userId)
            ? userId
            : null;
    }

    public async Task CompareAndSaveToChangelog(AppEntityType entityType, Guid entityId, object? before, object? after, CompareLogic? logicOverride = null, string? action = null, object? actionData = null)
    {
        var changeLogEntry = new ChangeLogEntry
        {
            UserId = GetCurrentUserId(),
            CreatedAt = DateTime.UtcNow,
            EntityId = entityId,
            EntityType = entityType,
            Action = action,
            ActionData = JsonSerializer.Serialize(actionData, _jsonOptions)
        };

        if (before == null)
        {
            changeLogEntry.ChangeLogEntryType = ChangeLogEntryType.Added;
            changeLogEntry.Snapshot = JsonSerializer.Serialize(after, _jsonOptions);
            changeLogEntry.Context = changeLogEntry.Snapshot;
            changeLogEntry.Diffs = [];
        }
        else if (after == null)
        {
            changeLogEntry.ChangeLogEntryType = ChangeLogEntryType.Deleted;
            changeLogEntry.Snapshot = JsonSerializer.Serialize(before, _jsonOptions);
            changeLogEntry.Context = changeLogEntry.Snapshot;
            changeLogEntry.Diffs = [];
        }
        else
        {
            var logic = logicOverride ?? GetCompareLogic();
            var diff = logic.Compare(before, after);

            if (diff.AreEqual) return;

            changeLogEntry.ChangeLogEntryType = ChangeLogEntryType.Modified;
            changeLogEntry.Snapshot = JsonSerializer.Serialize(before, _jsonOptions);
            changeLogEntry.Context = JsonSerializer.Serialize(after, _jsonOptions);
            changeLogEntry.Diffs = diff.Differences
                .Where(d => d.Object1TypeName != "List`1" && d.ChildPropertyName != "Count")
                .Select(d => new ChangeLogDiff
                {
                    Path = d.PropertyName,
                    From = d.Object1,
                    To = d.Object2,
                }).Reverse().ToList();
        }

        await AddNewEntry(changeLogEntry);

        // Watchers of the object are told only after the write went through, and never the author.
        await _realtime.PublishEntityChangedAsync(entityType, entityId, changeLogEntry.UserId,
            _httpContextAccessor.HttpContext?.User.GetDisplayName());
        
        _logger.LogInformation("Changelog entry created: {ChangeLogEntry}", changeLogEntry);
    }

    public IQueryable<ChangeLogEntry> GetChangelog(AppEntityType entityType, Guid entityId)
    {
        return GetGetChangelogQueryable()
            .Where(c => c.EntityId == entityId && c.EntityType == entityType)
            .OrderByDescending(c => c.CreatedAt);
    }
}

public interface IChangeLogService
{
    public Task CompareAndSaveToChangelog(AppEntityType entityType, Guid entityId, object? before, object? after, CompareLogic? logicOverride = null, string? action = null, object? actionData = null);
    public IQueryable<ChangeLogEntry> GetChangelog(AppEntityType entityType, Guid entityId);
}

public interface IChangeLogService<in T>
{
    public Task CompareAndSaveToChangelog(T? before, T? after, string? action = null, object? actionData = null);
    public IQueryable<ChangeLogEntry> GetChangelog(Guid entityId);
}
