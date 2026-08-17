using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Models.Stocktakes;

namespace ProjectWarehouse.Server.Infrastructure.ChangeLog;

public class StocktakeDtoChangelogService(IChangeLogService changeLogService) : IChangeLogService<StocktakeDto>
{
    private const AppEntityType EntityType = AppEntityType.Stocktake;

    public Task CompareAndSaveToChangelog(StocktakeDto? before, StocktakeDto? after, string? action = null,
        object? actionData = null)
    {
        var logic = AbstractChangeLogService.GetCompareLogic();
        return changeLogService.CompareAndSaveToChangelog(EntityType, before?.Id ?? after?.Id ?? Guid.Empty, before,
            after, logic, action, actionData);
    }

    public IQueryable<ChangeLogEntry> GetChangelog(Guid entityId) =>
        changeLogService.GetChangelog(EntityType, entityId);
}
