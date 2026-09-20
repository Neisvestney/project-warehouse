using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Models.Files;
using ProjectWarehouse.Server.Models.Users;

namespace ProjectWarehouse.Server.Infrastructure.ChangeLog;

public class UserDetailDtoChangelogService(IChangeLogService changeLogService): IChangeLogService<UserDetailDto>
{
    private const AppEntityType EntityType = AppEntityType.User;
    
    public Task CompareAndSaveToChangelog(UserDetailDto? before, UserDetailDto? after, string? action = null,
        object? actionData = null)
    {
        var logic = AbstractChangeLogService.GetCompareLogic();

        // Keep which file the avatar points at and drop the rest of its metadata — a file name or an
        // upload timestamp is not an edit to the user.
        foreach (var member in typeof(DataFileDto).GetProperties().Where(p => p.Name != nameof(DataFileDto.Id)))
            logic.Config.MembersToIgnore.Add($"{nameof(DataFileDto)}.{member.Name}");

        return changeLogService.CompareAndSaveToChangelog(EntityType, before?.Id ?? after?.Id ?? Guid.Empty, before, after, logic, action, actionData);
    }

    public IQueryable<ChangeLogEntry> GetChangelog(Guid entityId)
    {
        return changeLogService.GetChangelog(EntityType, entityId);
    }
}