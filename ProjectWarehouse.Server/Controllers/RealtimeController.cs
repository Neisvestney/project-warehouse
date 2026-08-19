using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using ProjectWarehouse.Server.Infrastructure;
using ProjectWarehouse.Server.Infrastructure.Realtime;
using ProjectWarehouse.Server.Models.Realtime;
using ProjectWarehouse.Server.Services;

namespace ProjectWarehouse.Server.Controllers;

[Route("api/realtime")]
public class RealtimeController(
    RealtimeConnectionManager connections,
    EntityWatchRegistry watchRegistry,
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

        var connection = connections.Register(userId.Value, User.Identity?.Name ?? string.Empty);

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
            connections.Remove(connection.Id);
            watchRegistry.RemoveConnection(connection.Id);
        }

        return new EmptyResult();
    }

    /// <summary>Subscribes the connection to one object's events. The right to view it is checked here, once.</summary>
    [HttpPost("watch")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Watch(RealtimeWatchRequest request, CancellationToken ct)
    {
        var connection = connections.Find(request.ConnectionId);
        if (connection is null || connection.UserId != GetCurrentUserId())
            return UnknownConnection();

        if (!await entityAccess.CanViewAsync(request.EntityType, request.EntityId, User, ct))
            return Forbidden();

        watchRegistry.Watch(connection.Id, request.EntityType, request.EntityId);

        // The stream may have dropped while the access check ran, after its own cleanup already passed —
        // without this the subscription would sit in the registry forever with nobody to remove it.
        if (connections.Find(request.ConnectionId) is null)
        {
            watchRegistry.RemoveConnection(connection.Id);
            return UnknownConnection();
        }

        return NoContent();
    }

    /// <summary>No permission check: dropping a subscription is always allowed.</summary>
    [HttpPost("unwatch")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public IActionResult Unwatch(RealtimeWatchRequest request)
    {
        var connection = connections.Find(request.ConnectionId);
        if (connection is not null && connection.UserId != GetCurrentUserId())
            return UnknownConnection();

        watchRegistry.Unwatch(request.ConnectionId, request.EntityType, request.EntityId);
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
