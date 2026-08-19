using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using ProjectWarehouse.Server.Infrastructure;
using ProjectWarehouse.Server.Infrastructure.Realtime;
using ProjectWarehouse.Server.Models;
using ProjectWarehouse.Server.Models.Realtime;
using ProjectWarehouse.Server.Services;

namespace ProjectWarehouse.Server.Controllers;

[Route("api/realtime")]
public class RealtimeController(
    RealtimeConnectionManager connections,
    EntityPresenceService presence,
    EditLockStore locks,
    IRealtimeNotifier realtime,
    IEntityAccessService entityAccess,
    IOptions<JsonOptions> jsonOptions) : AppControllerBase
{
    private static readonly TimeSpan KeepAliveInterval = TimeSpan.FromSeconds(20);

    /// <summary>
    /// The event stream. One per tab: HTTP/1.1 caps a browser at six connections per origin and a
    /// hanging stream occupies one of them.
    /// </summary>
    [HttpGet("stream")]
    [Authorize]
    [Produces("text/event-stream")]
    [ProducesResponseType<RealtimeEvent>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Stream(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
            return Unauthorized(ErrorCode.TokenInvalid, "Token does not contain a valid user ID.");

        Response.Headers.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers["X-Accel-Buffering"] = "no";
        // Without this Kestrel buffers the response and events arrive in batches.
        HttpContext.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();

        var connection = connections.Register(userId.Value, User.GetDisplayName());

        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(ct, connection.Aborted);

        // The token is validated once, when the stream opens; closing at its expiry is what keeps
        // SecurityVersion invalidation working for a long-lived connection.
        if (GetTokenLifetimeLeft() is { } left)
            lifetime.CancelAfter(left > TimeSpan.Zero ? left : TimeSpan.Zero);

        try
        {
            await WriteEventAsync(
                new RealtimeEvent { Payload = new ConnectionReadyPayload { ConnectionId = connection.Id } },
                lifetime.Token);

            await PumpAsync(connection, lifetime.Token);
        }
        // Both are the client hanging up: an aborted connection cancels, a broken pipe throws IOException.
        catch (Exception ex) when (ex is OperationCanceledException or IOException)
        {
        }
        finally
        {
            // Released before the registry is cleared so the events still reach this object's watchers.
            foreach (var released in locks.ReleaseByConnection(connection.Id))
                await realtime.PublishLockReleasedAsync(released, CancellationToken.None);

            connections.Remove(connection.Id);
            await presence.RemoveConnectionAsync(connection.Id, connection.UserId, CancellationToken.None);
        }

        return new EmptyResult();
    }

    /// <summary>
    /// Subscribes the connection to a batch of objects. The right to view each is checked here, once;
    /// the ones that pass come back in the response, the rest are simply absent.
    /// </summary>
    [HttpPost("watch")]
    [Authorize]
    [ProducesResponseType<RealtimeWatchResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Watch(RealtimeWatchRequest request, CancellationToken ct)
    {
        var connection = connections.Find(request.ConnectionId);
        if (connection is null || connection.UserId != GetCurrentUserId())
            return UnknownConnection();

        var watched = new List<RealtimeEntityRef>();
        foreach (var entity in request.Entities.DistinctBy(e => (e.EntityType, e.EntityId)))
        {
            if (!await entityAccess.CanViewAsync(entity.EntityType, entity.EntityId, User, ct)) continue;

            await presence.WatchAsync(connection.Id, entity.EntityType, entity.EntityId, ct);
            watched.Add(entity);
        }

        // The stream may have dropped while the access checks ran, after its own cleanup already passed —
        // without this the subscriptions would sit in the registry forever with nobody to remove them.
        if (connections.Find(request.ConnectionId) is null)
        {
            await presence.RemoveConnectionAsync(connection.Id, connection.UserId, ct);
            return UnknownConnection();
        }

        return Ok(new RealtimeWatchResponse
        {
            Watched = watched,
            Presence = watched.Select(e => new EntityPresenceDto
            {
                EntityType = e.EntityType,
                EntityId = e.EntityId,
                Viewers = presence.GetViewers(e.EntityType, e.EntityId),
            }).ToList(),
        });
    }

    /// <summary>No permission check: dropping a subscription is always allowed.</summary>
    [HttpPost("unwatch")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Unwatch(RealtimeWatchRequest request, CancellationToken ct)
    {
        var connection = connections.Find(request.ConnectionId);
        if (connection is not null && connection.UserId != GetCurrentUserId())
            return UnknownConnection();

        foreach (var entity in request.Entities)
            await presence.UnwatchAsync(request.ConnectionId, entity.EntityType, entity.EntityId, ct);

        return NoContent();
    }

    /// <summary>
    /// Keeps the connection alive. Writing to the stream proves nothing — a proxy between the browser and
    /// Kestrel keeps accepting bytes for a tab that is long gone — so the client has to say so itself.
    /// </summary>
    [HttpPost("heartbeat")]
    [Authorize]
    [ProducesResponseType<RealtimeHeartbeatResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public IActionResult Heartbeat(RealtimeHeartbeatRequest request)
    {
        var connection = connections.Find(request.ConnectionId);
        if (connection is null || connection.UserId != GetCurrentUserId())
            return UnknownConnection();

        connection.Touch();

        return Ok(new RealtimeHeartbeatResponse
        {
            Locks = locks.ByConnection(connection.Id).Select(EditLockDto.From).ToList(),
        });
    }

    // ── Edit locks ────────────────────────────────────────────────────────────

    /// <summary>
    /// Claims the object for editing. The lock is advisory: it warns other users and blocks no write,
    /// which is why <c>PUT</c> of the object never consults it.
    /// </summary>
    [HttpPost("locks/acquire")]
    [Authorize]
    [ProducesResponseType<EditLockDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> AcquireLock(RealtimeLockRequest request, CancellationToken ct)
    {
        var connection = connections.Find(request.ConnectionId);
        if (connection is null || connection.UserId != GetCurrentUserId())
            return UnknownConnection();

        // The same right that editing the object needs — a lock grants no access of its own.
        if (!await entityAccess.CanEditAsync(request.EntityType, request.EntityId, User, ct))
            return Forbidden();

        var (held, acquired) = locks.Acquire(request.EntityType, request.EntityId, connection.UserId,
            connection.UserName, connection.Id);

        if (!acquired)
            return Problem(AppProblems.Root(StatusCodes.Status409Conflict, ErrorCode.EditLockHeld,
                "The object is being edited by another user.", new Dictionary<string, object>
                {
                    ["userId"] = held.UserId,
                    ["userName"] = held.UserName,
                }));

        // Same race as in Watch: the stream may have dropped while the access check ran.
        if (connections.Find(request.ConnectionId) is null)
        {
            foreach (var orphaned in locks.ReleaseByConnection(connection.Id))
                await realtime.PublishLockReleasedAsync(orphaned, ct);

            return UnknownConnection();
        }

        await realtime.PublishLockAcquiredAsync(held, ct);
        return Ok(EditLockDto.From(held));
    }

    [HttpPost("locks/release")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<AppProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ReleaseLock(RealtimeLockRequest request, CancellationToken ct)
    {
        var connection = connections.Find(request.ConnectionId);
        if (connection is null || connection.UserId != GetCurrentUserId())
            return UnknownConnection();

        var released = locks.Release(request.EntityType, request.EntityId, connection.Id);
        if (released is null)
            return Conflict(ErrorCode.EditLockNotHeld, "This connection does not hold the lock.");

        await realtime.PublishLockReleasedAsync(released, ct);
        return NoContent();
    }

    private ObjectResult UnknownConnection() =>
        UnprocessableEntity("connectionId", ErrorCode.RealtimeConnectionUnknown,
            "Realtime connection is unknown or already closed.");

    private TimeSpan? GetTokenLifetimeLeft()
    {
        var raw = User.FindFirst(JwtRegisteredClaimNames.Exp)?.Value;
        return long.TryParse(raw, out var exp)
            ? DateTimeOffset.FromUnixTimeSeconds(exp) - DateTimeOffset.UtcNow
            : null;
    }

    private async Task PumpAsync(RealtimeConnection connection, CancellationToken ct)
    {
        using var keepAlive = new PeriodicTimer(KeepAliveInterval);

        var pending = connection.Reader.WaitToReadAsync(ct).AsTask();
        var tick = keepAlive.WaitForNextTickAsync(ct).AsTask();

        while (!ct.IsCancellationRequested)
        {
            if (await Task.WhenAny(pending, tick) == pending)
            {
                if (!await pending) return;

                while (connection.Reader.TryRead(out var evt))
                    await WriteEventAsync(evt, ct);

                pending = connection.Reader.WaitToReadAsync(ct).AsTask();
            }
            else
            {
                if (!await tick) return;

                // Proxies and mobile networks drop silent connections; the comment is ignored by the client.
                await Response.WriteAsync(":ping\n\n", ct);
                await Response.Body.FlushAsync(ct);

                tick = keepAlive.WaitForNextTickAsync(ct).AsTask();
            }
        }
    }

    private async Task WriteEventAsync(RealtimeEvent evt, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(evt, jsonOptions.Value.JsonSerializerOptions);

        await Response.WriteAsync($"data: {json}\n\n", ct);
        await Response.Body.FlushAsync(ct);
    }
}
