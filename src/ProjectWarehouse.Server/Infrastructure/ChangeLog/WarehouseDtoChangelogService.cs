using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Models.Warehouses;

namespace ProjectWarehouse.Server.Infrastructure.ChangeLog;

public class WarehouseDtoChangelogService(IChangeLogService changeLogService)
    : TypedChangeLogService<WarehouseDto>(changeLogService, AppEntityType.Warehouse);
