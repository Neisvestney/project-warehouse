using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Models.InboundOrders;

namespace ProjectWarehouse.Server.Infrastructure.ChangeLog;

public class InboundOrderDtoChangelogService(IChangeLogService changeLogService) : IChangeLogService<InboundOrderDto>
{
    private const AppEntityType EntityType = AppEntityType.InboundOrder;

    public Task CompareAndSaveToChangelog(InboundOrderDto? before, InboundOrderDto? after,
        string? action = null, object? actionData = null)
    {
        var logic = AbstractChangeLogService.GetCompareLogic();
        return changeLogService.CompareAndSaveToChangelog(
            EntityType, before?.Id ?? after?.Id ?? Guid.Empty,
            before, after, logic, action, actionData);
    }

    public IQueryable<ChangeLogEntry> GetChangelog(Guid entityId) =>
        changeLogService.GetChangelog(EntityType, entityId);
}
