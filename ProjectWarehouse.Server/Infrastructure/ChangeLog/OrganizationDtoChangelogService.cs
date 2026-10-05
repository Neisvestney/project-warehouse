using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Models.Organizations;

namespace ProjectWarehouse.Server.Infrastructure.ChangeLog;

public class OrganizationDtoChangelogService(IChangeLogService changeLogService) : IChangeLogService<OrganizationDto>
{
    private const AppEntityType EntityType = AppEntityType.Organization;

    public Task CompareAndSaveToChangelog(OrganizationDto? before, OrganizationDto? after,
        string? action = null, object? actionData = null)
    {
        var logic = AbstractChangeLogService.GetCompareLogic();
        return changeLogService.CompareAndSaveToChangelog(EntityType, before?.Id ?? after?.Id ?? Guid.Empty, before,
            after, logic, action, actionData);
    }

    public IQueryable<ChangeLogEntry> GetChangelog(Guid entityId) =>
        changeLogService.GetChangelog(EntityType, entityId);
}
