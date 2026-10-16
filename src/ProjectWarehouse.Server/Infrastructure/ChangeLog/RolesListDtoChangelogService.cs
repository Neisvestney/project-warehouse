using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Models.Roles;

namespace ProjectWarehouse.Server.Infrastructure.ChangeLog;

public class RolesListDtoChangelogService(IChangeLogService changeLogService) : IChangeLogService<RolesListDto>
{
    private const AppEntityType EntityType = AppEntityType.Roles;

    public Task CompareAndSaveToChangelog(RolesListDto? before, RolesListDto? after, string? action = null,
        object? actionData = null)
    {
        var logic = AbstractChangeLogService.GetCompareLogic();
        return changeLogService.CompareAndSaveToChangelog(EntityType, Guid.Empty, before, after, logic, action,
            actionData);
    }

    public IQueryable<ChangeLogEntry> GetChangelog(Guid entityId) =>
        changeLogService.GetChangelog(EntityType, Guid.Empty);
}
