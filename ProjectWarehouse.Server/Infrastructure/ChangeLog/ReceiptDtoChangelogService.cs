using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Models.Receipts;

namespace ProjectWarehouse.Server.Infrastructure.ChangeLog;

public class ReceiptDtoChangelogService(IChangeLogService changeLogService) : IChangeLogService<ReceiptDto>
{
    private const AppEntityType EntityType = AppEntityType.Receipt;

    public Task CompareAndSaveToChangelog(ReceiptDto? before, ReceiptDto? after, string? action = null,
        object? actionData = null)
    {
        var logic = AbstractChangeLogService.GetCompareLogic();
        return changeLogService.CompareAndSaveToChangelog(EntityType, before?.Id ?? after?.Id ?? Guid.Empty, before,
            after, logic, action, actionData);
    }

    public IQueryable<ChangeLogEntry> GetChangelog(Guid entityId) =>
        changeLogService.GetChangelog(EntityType, entityId);
}
