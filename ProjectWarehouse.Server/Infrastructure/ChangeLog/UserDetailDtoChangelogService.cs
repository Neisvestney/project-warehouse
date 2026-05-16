using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Models.Users;

namespace ProjectWarehouse.Server.Infrastructure.ChangeLog;

public class UserDetailDtoChangelogService(IChangeLogService changeLogService): IChangeLogService<UserDetailDto>
{
    private const AppEntityType EntityType = AppEntityType.User;
    
    public Task CompareAndSaveToChangelog(UserDetailDto? before, UserDetailDto? after, string? action = null,
        object? actionData = null)
    {
        var logic = AbstractChangeLogService.GetCompareLogic();
        return changeLogService.CompareAndSaveToChangelog(EntityType, before?.Id ?? after?.Id ?? Guid.Empty, before, after, logic, action, actionData);
    }

    public IQueryable<ChangeLogEntry> GetChangelog(Guid entityId)
    {
        return changeLogService.GetChangelog(EntityType, entityId);
    }
}