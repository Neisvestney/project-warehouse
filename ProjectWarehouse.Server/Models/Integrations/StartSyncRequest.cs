using ProjectWarehouse.Server.Domain;

namespace ProjectWarehouse.Server.Models.Integrations;

public class StartSyncRequest
{
    public MarketplaceSyncScope Scope { get; init; } = MarketplaceSyncScope.All;

    /// <summary>
    /// Period to import history for. Required by <see cref="MarketplaceSyncScope.OrdersBackfill"/> and
    /// rejected by every other scope — nothing else reads a period.
    /// </summary>
    public DateTime? Since { get; init; }
    public DateTime? To { get; init; }
}

public class StartSyncResponse
{
    public Guid SyncRunId { get; init; }
}

/// <summary>Where a history import can reasonably start. Both ends are null when there is nothing to go on.</summary>
public class BackfillBoundsDto
{
    /// <summary>Creation date of the oldest order WMS has for this account.</summary>
    public DateTime? FirstOrderAt { get; init; }

    /// <summary>
    /// Creation date of the oldest posting the marketplace still knows about. Only filled when asked for
    /// explicitly — finding it costs several marketplace calls.
    /// </summary>
    public DateTime? FirstPostingAt { get; init; }
}
