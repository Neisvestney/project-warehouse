using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Models.Roles;

namespace ProjectWarehouse.Server.Infrastructure.ChangeLog;

public class RolesListDtoChangelogService(IChangeLogService changeLogService)
    : TypedChangeLogService<RolesListDto>(changeLogService, AppEntityType.Roles)
{
    protected override Guid NormalizeEntityId(Guid entityId) => Guid.Empty;
}
