using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Models.Writeoffs;

namespace ProjectWarehouse.Server.Infrastructure.ChangeLog;

public class WriteoffDtoChangelogService(IChangeLogService changeLogService) : IChangeLogService<WriteoffDto>
{
    private const AppEntityType EntityType = AppEntityType.Writeoff;

    public Task CompareAndSaveToChangelog(WriteoffDto? before, WriteoffDto? after, string? action = null,
        object? actionData = null)
    {
        var logic = AbstractChangeLogService.GetCompareLogic();
        return changeLogService.CompareAndSaveToChangelog(EntityType, before?.Id ?? after?.Id ?? Guid.Empty, before,
            after, logic, action, actionData);
    }

    public IQueryable<ChangeLogEntry> GetChangelog(Guid entityId) =>
        changeLogService.GetChangelog(EntityType, entityId);
}
