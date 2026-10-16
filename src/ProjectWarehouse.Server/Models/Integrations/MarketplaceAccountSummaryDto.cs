using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Infrastructure;

namespace ProjectWarehouse.Server.Models.Integrations;

public class MarketplaceAccountSummaryDto : IHasIdentity
{
    public Guid Id { get; init; }
    public MarketplaceType Type { get; init; }
    public string Name { get; init; } = null!;
    public bool IsActive { get; init; }
    public int SyncIntervalMinutes { get; init; }
    public DateTime? LastSyncAt { get; init; }
    public MarketplaceSyncStatus? LastSyncStatus { get; init; }
    public AppFieldError? LastSyncError { get; init; }

    public int WarehouseCount { get; init; }
    public int CardCount { get; init; }
    public int UnmappedCardCount { get; init; }
}
