namespace ProjectWarehouse.Server.Models.Realtime;

/// <summary>One heartbeat for the whole connection: everything it holds lives and dies with it.</summary>
public class RealtimeHeartbeatRequest
{
    public required Guid ConnectionId { get; init; }
}

public class RealtimeHeartbeatResponse
{
    /// <summary>
    /// The locks still held. A client compares it against what it thinks it owns — a lock taken over by
    /// another tab of the same user simply stops being listed, and no event announces that.
    /// </summary>
    public required IReadOnlyList<EditLockDto> Locks { get; init; }
}
