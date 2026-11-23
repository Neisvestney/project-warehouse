using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Models.Tags;

namespace ProjectWarehouse.Server.Infrastructure.ChangeLog;

public class TagDtoChangelogService(IChangeLogService changeLogService)
    : TypedChangeLogService<TagDto>(changeLogService, AppEntityType.Tag);
