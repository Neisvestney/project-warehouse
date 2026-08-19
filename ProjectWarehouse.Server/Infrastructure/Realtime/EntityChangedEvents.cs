using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using ProjectWarehouse.Server.Domain;

namespace ProjectWarehouse.Server.Infrastructure.Realtime;

/// <summary>
/// Publishing helpers for <see cref="EntityChangedPayload"/>. The changelog service covers every entity
/// with a registered changelog; entities without one (orders) call these directly from their controller.
/// </summary>
public static class EntityChangedEvents
{
    public static ValueTask PublishEntityChangedAsync(this IRealtimeNotifier notifier, AppEntityType entityType,
        Guid entityId, Guid? byUserId, string? byUserName, CancellationToken ct = default) =>
        notifier.PublishAsync(RealtimeAddress.ToWatchers(entityType, entityId, byUserId), new RealtimeEvent
        {
            Payload = new EntityChangedPayload
            {
                EntityType = entityType,
                EntityId = entityId,
                ByUserId = byUserId,
                ByUserName = byUserName,
            },
        }, ct);

    /// <summary>Same, taking the author from the current principal.</summary>
    public static ValueTask PublishEntityChangedAsync(this IRealtimeNotifier notifier, AppEntityType entityType,
        Guid entityId, ClaimsPrincipal? user, CancellationToken ct = default) =>
        notifier.PublishEntityChangedAsync(entityType, entityId, GetUserId(user), user.GetDisplayName(), ct);

    private static Guid? GetUserId(ClaimsPrincipal? user) =>
        Guid.TryParse(user?.FindFirstValue(JwtRegisteredClaimNames.Sub), out var id) ? id : null;
}
