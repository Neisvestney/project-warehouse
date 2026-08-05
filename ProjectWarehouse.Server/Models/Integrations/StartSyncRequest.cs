using ProjectWarehouse.Server.Domain;

namespace ProjectWarehouse.Server.Models.Integrations;

public class StartSyncRequest
{
    public MarketplaceSyncScope Scope { get; init; } = MarketplaceSyncScope.All;
}

public class StartSyncResponse
{
    public Guid SyncRunId { get; init; }
}
