using KellermanSoftware.CompareNetObjects;
using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Models.Files;
using ProjectWarehouse.Server.Models.Users;

namespace ProjectWarehouse.Server.Infrastructure.ChangeLog;

public class UserDetailDtoChangelogService(IChangeLogService changeLogService)
    : TypedChangeLogService<UserDetailDto>(changeLogService, AppEntityType.User)
{
    protected override CompareLogic CreateCompareLogic()
    {
        var logic = base.CreateCompareLogic();

        // Keep which file the avatar points at and drop the rest of its metadata — a file name or an
        // upload timestamp is not an edit to the user.
        foreach (var member in typeof(DataFileDto).GetProperties().Where(p => p.Name != nameof(DataFileDto.Id)))
            logic.Config.MembersToIgnore.Add($"{nameof(DataFileDto)}.{member.Name}");

        return logic;
    }
}
