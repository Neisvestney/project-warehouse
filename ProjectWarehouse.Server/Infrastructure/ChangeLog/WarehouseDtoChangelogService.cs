using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Models.Warehouses;

namespace ProjectWarehouse.Server.Infrastructure.ChangeLog;

public class WarehouseDtoChangelogService(IChangeLogService changeLogService) : IChangeLogService<WarehouseDto>
{
    private const AppEntityType EntityType = AppEntityType.Warehouse;

    public Task CompareAndSaveToChangelog(WarehouseDto? before, WarehouseDto? after, string? action = null,
        object? actionData = null)
    {
        var logic = AbstractChangeLogService.GetCompareLogic();
        return changeLogService.CompareAndSaveToChangelog(EntityType, before?.Id ?? after?.Id ?? Guid.Empty, before,
            after, logic, action, actionData);
    }

    public IQueryable<ChangeLogEntry> GetChangelog(Guid entityId) =>
        changeLogService.GetChangelog(EntityType, entityId);
}
