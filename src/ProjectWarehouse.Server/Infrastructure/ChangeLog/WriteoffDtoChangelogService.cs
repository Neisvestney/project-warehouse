using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Models.Writeoffs;

namespace ProjectWarehouse.Server.Infrastructure.ChangeLog;

public class WriteoffDtoChangelogService(IChangeLogService changeLogService)
    : TypedChangeLogService<WriteoffDto>(changeLogService, AppEntityType.Writeoff);
