using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Models.Warehouses;

namespace ProjectWarehouse.Server.Infrastructure.ChangeLog;

public class StoragePlaceNodeDetailsDtoChangelogService(IChangeLogService changeLogService)
    : TypedChangeLogService<StoragePlaceNodeDetailsDto>(changeLogService, AppEntityType.StoragePlaceNode);
