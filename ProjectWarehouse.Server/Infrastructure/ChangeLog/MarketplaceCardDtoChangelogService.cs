using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Models.Integrations;

namespace ProjectWarehouse.Server.Infrastructure.ChangeLog;

public class MarketplaceCardDtoChangelogService(IChangeLogService changeLogService)
    : IChangeLogService<MarketplaceCardDto>
{
    private const AppEntityType EntityType = AppEntityType.MarketplaceCard;

    public Task CompareAndSaveToChangelog(MarketplaceCardDto? before, MarketplaceCardDto? after,
        string? action = null, object? actionData = null)
    {
        var logic = AbstractChangeLogService.GetCompareLogic();
        return changeLogService.CompareAndSaveToChangelog(EntityType, before?.Id ?? after?.Id ?? Guid.Empty, before,
            after, logic, action, actionData);
    }

    public IQueryable<ChangeLogEntry> GetChangelog(Guid entityId) =>
        changeLogService.GetChangelog(EntityType, entityId);
}
