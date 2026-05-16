using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Models.Warehouses;

namespace ProjectWarehouse.Server.Infrastructure.ChangeLog;

public class StoragePlaceNodeDetailsDtoChangelogService(IChangeLogService changeLogService)
    : IChangeLogService<StoragePlaceNodeDetailsDto>
{
    private const AppEntityType EntityType = AppEntityType.StoragePlaceNode;

    public Task CompareAndSaveToChangelog(StoragePlaceNodeDetailsDto? before, StoragePlaceNodeDetailsDto? after,
        string? action = null, object? actionData = null)
    {
        var logic = AbstractChangeLogService.GetCompareLogic();
        return changeLogService.CompareAndSaveToChangelog(EntityType, before?.Id ?? after?.Id ?? Guid.Empty, before,
            after, logic, action, actionData);
    }

    public IQueryable<ChangeLogEntry> GetChangelog(Guid entityId) =>
        changeLogService.GetChangelog(EntityType, entityId);
}
