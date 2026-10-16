using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Models.Tags;

namespace ProjectWarehouse.Server.Infrastructure.ChangeLog;

public class TagDtoChangelogService(IChangeLogService changeLogService) : IChangeLogService<TagDto>
{
    private const AppEntityType EntityType = AppEntityType.Tag;

    public Task CompareAndSaveToChangelog(TagDto? before, TagDto? after,
        string? action = null, object? actionData = null)
    {
        var logic = AbstractChangeLogService.GetCompareLogic();
        return changeLogService.CompareAndSaveToChangelog(EntityType, before?.Id ?? after?.Id ?? Guid.Empty, before,
            after, logic, action, actionData);
    }

    public IQueryable<ChangeLogEntry> GetChangelog(Guid entityId) =>
        changeLogService.GetChangelog(EntityType, entityId);
}
