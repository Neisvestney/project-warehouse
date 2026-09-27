using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Models.Analytics;

namespace ProjectWarehouse.Server.Infrastructure.ChangeLog;

public class AnalyticsSettingsDtoChangelogService(IChangeLogService changeLogService)
    : IChangeLogService<AnalyticsSettingsDto>
{
    private const AppEntityType EntityType = AppEntityType.AnalyticsSettings;

    public Task CompareAndSaveToChangelog(AnalyticsSettingsDto? before, AnalyticsSettingsDto? after,
        string? action = null, object? actionData = null)
    {
        var logic = AbstractChangeLogService.GetCompareLogic();
        // Bookkeeping moves on every save, so a save that changed no value would otherwise still be journalled
        logic.Config.MembersToIgnore.Add($"{nameof(AnalyticsSettingsDto)}.{nameof(AnalyticsSettingsDto.Version)}");
        logic.Config.MembersToIgnore.Add($"{nameof(AnalyticsSettingsDto)}.{nameof(AnalyticsSettingsDto.UpdatedAt)}");
        logic.Config.MembersToIgnore.Add($"{nameof(AnalyticsSettingsDto)}.{nameof(AnalyticsSettingsDto.UpdatedByName)}");
        return changeLogService.CompareAndSaveToChangelog(EntityType, before?.Id ?? after?.Id ?? Guid.Empty, before,
            after, logic, action, actionData);
    }

    public IQueryable<ChangeLogEntry> GetChangelog(Guid entityId) =>
        changeLogService.GetChangelog(EntityType, entityId);
}
