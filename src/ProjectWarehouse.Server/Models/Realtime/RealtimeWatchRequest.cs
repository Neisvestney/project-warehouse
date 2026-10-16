using System.ComponentModel.DataAnnotations;
using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Infrastructure.Realtime;

namespace ProjectWarehouse.Server.Models.Realtime;

public class RealtimeEntityRef
{
    public required AppEntityType EntityType { get; init; }

    public required Guid EntityId { get; init; }
}

/// <summary>
/// Subscriptions come in batches: a screen listing thirty orders would otherwise open thirty requests
/// the moment it mounts, and the browser caps them at six per origin.
/// </summary>
public class RealtimeWatchRequest
{
    /// <summary>From the connectionReady event of the stream these subscriptions belong to.</summary>
    public required Guid ConnectionId { get; init; }

    [MaxLength(200)]
    public required IReadOnlyList<RealtimeEntityRef> Entities { get; init; }
}

/// <summary>
/// Only the entities the user may actually view. Anything missing was refused, and the client leaves
/// those on their polling fallback instead of treating the whole batch as failed.
/// </summary>
public class RealtimeWatchResponse
{
    public required IReadOnlyList<RealtimeEntityRef> Watched { get; init; }

    /// <summary>
    /// Who is looking at each watched object right now. Seeding from the response closes the window
    /// between subscribing and the first entityPresenceChanged event.
    /// </summary>
    public required IReadOnlyList<EntityPresenceDto> Presence { get; init; }
}

public class EntityPresenceDto
{
    public required AppEntityType EntityType { get; init; }

    public required Guid EntityId { get; init; }

    public required IReadOnlyList<RealtimeViewer> Viewers { get; init; }
}
