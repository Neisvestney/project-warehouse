using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Models.Statistics;

namespace ProjectWarehouse.Server.Infrastructure.ChangeLog;

public class StockMovementReportPresetDtoChangelogService(IChangeLogService changeLogService)
    : IChangeLogService<StockMovementReportPresetDto>
{
    private const AppEntityType EntityType = AppEntityType.StockMovementReportPreset;

    public Task CompareAndSaveToChangelog(StockMovementReportPresetDto? before, StockMovementReportPresetDto? after,
        string? action = null, object? actionData = null)
    {
        var logic = AbstractChangeLogService.GetCompareLogic();
        return changeLogService.CompareAndSaveToChangelog(EntityType, before?.Id ?? after?.Id ?? Guid.Empty, before,
            after, logic, action, actionData);
    }

    public IQueryable<ChangeLogEntry> GetChangelog(Guid entityId) =>
        changeLogService.GetChangelog(EntityType, entityId);
}
