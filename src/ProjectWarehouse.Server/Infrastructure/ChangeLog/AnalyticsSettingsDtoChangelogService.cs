using KellermanSoftware.CompareNetObjects;
using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Models.Analytics;

namespace ProjectWarehouse.Server.Infrastructure.ChangeLog;

public class AnalyticsSettingsDtoChangelogService(IChangeLogService changeLogService)
    : TypedChangeLogService<AnalyticsSettingsDto>(changeLogService, AppEntityType.AnalyticsSettings)
{
    protected override CompareLogic CreateCompareLogic()
    {
        var logic = base.CreateCompareLogic();
        // Bookkeeping moves on every save, so a save that changed no value would otherwise still be journalled
        logic.Config.MembersToIgnore.Add($"{nameof(AnalyticsSettingsDto)}.{nameof(AnalyticsSettingsDto.Version)}");
        logic.Config.MembersToIgnore.Add($"{nameof(AnalyticsSettingsDto)}.{nameof(AnalyticsSettingsDto.UpdatedAt)}");
        logic.Config.MembersToIgnore.Add($"{nameof(AnalyticsSettingsDto)}.{nameof(AnalyticsSettingsDto.UpdatedByName)}");
        return logic;
    }
}
