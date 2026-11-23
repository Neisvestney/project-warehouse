using KellermanSoftware.CompareNetObjects;
using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Models.Catalog;
using ProjectWarehouse.Server.Models.Files;

namespace ProjectWarehouse.Server.Infrastructure.ChangeLog;

public class CatalogItemDtoChangelogService(IChangeLogService changeLogService)
    : TypedChangeLogService<CatalogItemDto>(changeLogService, AppEntityType.CatalogItem)
{
    protected override CompareLogic CreateCompareLogic()
    {
        var logic = base.CreateCompareLogic();

        // MainImage is the *effective* value: without this, changing the group's photo would log a
        // phantom edit on every child. The item's own MainImageFileId still diffs.
        logic.Config.MembersToIgnore.Add($"{nameof(CatalogItemDto)}.{nameof(CatalogItemDto.MainImage)}");
        // derived from the text fields, whose own diffs already record the edit
        logic.Config.MembersToIgnore.Add($"{nameof(CatalogItemDto)}.{nameof(CatalogItemDto.HasWhitespaceIssues)}");

        // In the images list keep which file is attached and drop the rest of its metadata — file
        // names and upload timestamps are not edits to the catalog item.
        foreach (var member in typeof(DataFileDto).GetProperties().Where(p => p.Name != nameof(DataFileDto.Id)))
            logic.Config.MembersToIgnore.Add($"{nameof(DataFileDto)}.{member.Name}");

        return logic;
    }
}
