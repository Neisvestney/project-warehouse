using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Models.Catalog;

namespace ProjectWarehouse.Server.Infrastructure.ChangeLog;

public class CatalogItemDtoChangelogService(IChangeLogService changeLogService) : IChangeLogService<CatalogItemDto>
{
    private const AppEntityType EntityType = AppEntityType.CatalogItem;

    public Task CompareAndSaveToChangelog(CatalogItemDto? before, CatalogItemDto? after, string? action = null,
        object? actionData = null)
    {
        var logic = AbstractChangeLogService.GetCompareLogic();
        return changeLogService.CompareAndSaveToChangelog(EntityType, before?.Id ?? after?.Id ?? Guid.Empty, before,
            after, logic, action, actionData);
    }

    public IQueryable<ChangeLogEntry> GetChangelog(Guid entityId) =>
        changeLogService.GetChangelog(EntityType, entityId);
}
