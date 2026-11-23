using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Models.Organizations;

namespace ProjectWarehouse.Server.Infrastructure.ChangeLog;

public class OrganizationDtoChangelogService(IChangeLogService changeLogService)
    : TypedChangeLogService<OrganizationDto>(changeLogService, AppEntityType.Organization);
