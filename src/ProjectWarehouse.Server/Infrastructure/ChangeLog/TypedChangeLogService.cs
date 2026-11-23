using KellermanSoftware.CompareNetObjects;
using ProjectWarehouse.Server.Domain;

namespace ProjectWarehouse.Server.Infrastructure.ChangeLog;

public abstract class TypedChangeLogService<T>(IChangeLogService changeLogService, AppEntityType entityType)
    : IChangeLogService<T> where T : class, IHasIdentity
{
    public Task CompareAndSaveToChangelog(T? before, T? after, string? action = null, object? actionData = null) =>
        changeLogService.CompareAndSaveToChangelog(entityType, NormalizeEntityId(before?.Id ?? after?.Id ?? Guid.Empty),
            before, after, CreateCompareLogic(), action, actionData);

    public Task CompareAndSaveBatchToChangelog(IEnumerable<ChangeLogChange<T>> changes, CancellationToken ct = default) =>
        changeLogService.CompareAndSaveBatchToChangelog(changes.Select(change => new ChangeLogComparison(
            entityType, NormalizeEntityId(change.Before?.Id ?? change.After?.Id ?? Guid.Empty), change.Before, change.After,
            CreateCompareLogic(), change.Action, change.ActionData)), ct);

    public IQueryable<ChangeLogEntry> GetChangelog(Guid entityId) =>
        changeLogService.GetChangelog(entityType, NormalizeEntityId(entityId));

    protected virtual CompareLogic CreateCompareLogic() => AbstractChangeLogService.GetCompareLogic();

    protected virtual Guid NormalizeEntityId(Guid entityId) => entityId;
}
