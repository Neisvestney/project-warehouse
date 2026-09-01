using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using ProjectWarehouse.Server.Domain;

namespace ProjectWarehouse.Server.Infrastructure.Realtime;

/// <summary>
/// Publishing helpers for <see cref="EntityChangedPayload"/>. The changelog service covers every entity
/// with a registered changelog; entities without one (orders) call these directly from their controller.
/// </summary>
public static class EntityChangedEvents
{
    /// <summary>
    /// Header a mutating request carries to name the connection (tab/device) that made it, so the fan-out
    /// can skip that one connection — other tabs or devices of the same user still get the event.
    /// </summary>
    public const string ConnectionIdHeader = "X-Realtime-Connection-Id";

    public static ValueTask PublishEntityChangedAsync(this IRealtimeNotifier notifier, AppEntityType entityType,
        Guid entityId, Guid? byUserId, string? byUserName, Guid? exceptConnectionId, CancellationToken ct = default) =>
        notifier.PublishAsync(RealtimeAddress.ToWatchers(entityType, entityId, exceptConnectionId: exceptConnectionId),
            new RealtimeEvent
            {
                Payload = new EntityChangedPayload
                {
                    EntityType = entityType,
                    EntityId = entityId,
                    ByUserId = byUserId,
                    ByUserName = byUserName,
                },
            }, ct);

    /// <summary>Same, taking the author and originating connection from the current request.</summary>
    public static ValueTask PublishEntityChangedAsync(this IRealtimeNotifier notifier, AppEntityType entityType,
        Guid entityId, HttpContext? httpContext, CancellationToken ct = default) =>
        notifier.PublishEntityChangedAsync(entityType, entityId, GetUserId(httpContext?.User),
            httpContext?.User.GetDisplayName(), GetConnectionId(httpContext), ct);

    public static Guid? GetConnectionId(HttpContext? httpContext) =>
        Guid.TryParse(httpContext?.Request.Headers[ConnectionIdHeader], out var id) ? id : null;

    private static Guid? GetUserId(ClaimsPrincipal? user) =>
        Guid.TryParse(user?.FindFirstValue(JwtRegisteredClaimNames.Sub), out var id) ? id : null;
}
