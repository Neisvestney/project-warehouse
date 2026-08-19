using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Infrastructure.Realtime;

namespace ProjectWarehouse.Server.Models.Realtime;

/// <summary>What the client needs to render "being edited by …".</summary>
public class EditLockDto
{
    public required AppEntityType EntityType { get; init; }

    public required Guid EntityId { get; init; }

    public required Guid UserId { get; init; }

    public required string UserName { get; init; }

    public static EditLockDto From(EditLock @lock) => new()
    {
        EntityType = @lock.EntityType,
        EntityId = @lock.EntityId,
        UserId = @lock.UserId,
        UserName = @lock.UserName,
    };
}
