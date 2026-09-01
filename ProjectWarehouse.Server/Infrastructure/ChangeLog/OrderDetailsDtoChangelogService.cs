using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Models.Orders;

namespace ProjectWarehouse.Server.Infrastructure.ChangeLog;

public class OrderDetailsDtoChangelogService(IChangeLogService changeLogService) : IChangeLogService<OrderDetailsDto>
{
    private const AppEntityType EntityType = AppEntityType.Order;

    public Task CompareAndSaveToChangelog(OrderDetailsDto? before, OrderDetailsDto? after, string? action = null,
        object? actionData = null)
    {
        var logic = AbstractChangeLogService.GetCompareLogic();
        return changeLogService.CompareAndSaveToChangelog(EntityType, before?.Id ?? after?.Id ?? Guid.Empty, before,
            after, logic, action, actionData);
    }

    public IQueryable<ChangeLogEntry> GetChangelog(Guid entityId) =>
        changeLogService.GetChangelog(EntityType, entityId);
}
