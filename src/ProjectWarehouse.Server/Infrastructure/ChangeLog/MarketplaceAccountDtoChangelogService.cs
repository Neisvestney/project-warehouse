using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Models.Integrations;

namespace ProjectWarehouse.Server.Infrastructure.ChangeLog;

public class MarketplaceAccountDtoChangelogService(IChangeLogService changeLogService)
    : IChangeLogService<MarketplaceAccountDto>
{
    private const AppEntityType EntityType = AppEntityType.MarketplaceAccount;

    public Task CompareAndSaveToChangelog(MarketplaceAccountDto? before, MarketplaceAccountDto? after,
        string? action = null, object? actionData = null)
    {
        var logic = AbstractChangeLogService.GetCompareLogic();
        return changeLogService.CompareAndSaveToChangelog(EntityType, before?.Id ?? after?.Id ?? Guid.Empty, before,
            after, logic, action, actionData);
    }

    public IQueryable<ChangeLogEntry> GetChangelog(Guid entityId) =>
        changeLogService.GetChangelog(EntityType, entityId);
}
