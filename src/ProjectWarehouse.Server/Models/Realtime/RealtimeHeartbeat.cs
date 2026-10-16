namespace ProjectWarehouse.Server.Models.Realtime;

/// <summary>One heartbeat for the whole connection: everything it holds lives and dies with it.</summary>
public class RealtimeHeartbeatRequest
{
    public required Guid ConnectionId { get; init; }
}

public class RealtimeHeartbeatResponse
{
    /// <summary>
    /// Whether this connection still holds any edit lock. A tab that is editing keeps its subscriptions
    /// while backgrounded instead of dropping them after twenty seconds, and this is how it finds out.
    /// The objects themselves are not listed: nobody asks, since a lock can no longer be taken away.
    /// </summary>
    public required bool HoldsLocks { get; init; }
}
