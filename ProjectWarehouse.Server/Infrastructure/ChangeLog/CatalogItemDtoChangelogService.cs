using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Models.Catalog;
using ProjectWarehouse.Server.Models.Files;

namespace ProjectWarehouse.Server.Infrastructure.ChangeLog;

public class CatalogItemDtoChangelogService(IChangeLogService changeLogService) : IChangeLogService<CatalogItemDto>
{
    private const AppEntityType EntityType = AppEntityType.CatalogItem;

    public Task CompareAndSaveToChangelog(CatalogItemDto? before, CatalogItemDto? after, string? action = null,
        object? actionData = null)
    {
        var logic = AbstractChangeLogService.GetCompareLogic();

        // MainImage is the *effective* value: without this, changing the group's photo would log a
        // phantom edit on every child. The item's own MainImageFileId still diffs.
        logic.Config.MembersToIgnore.Add($"{nameof(CatalogItemDto)}.{nameof(CatalogItemDto.MainImage)}");

        // In the images list keep which file is attached and drop the rest of its metadata — file
        // names and upload timestamps are not edits to the catalog item.
        foreach (var member in typeof(DataFileDto).GetProperties().Where(p => p.Name != nameof(DataFileDto.Id)))
            logic.Config.MembersToIgnore.Add($"{nameof(DataFileDto)}.{member.Name}");

        return changeLogService.CompareAndSaveToChangelog(EntityType, before?.Id ?? after?.Id ?? Guid.Empty, before,
            after, logic, action, actionData);
    }

    public IQueryable<ChangeLogEntry> GetChangelog(Guid entityId) =>
        changeLogService.GetChangelog(EntityType, entityId);
}
