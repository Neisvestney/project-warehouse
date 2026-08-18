namespace ProjectWarehouse.Server.Models.Integrations;

public enum MarketplaceAccountSortBy
{
    Name = 0,
    CreatedAt = 1,
    LastSyncAt = 2,
}

public enum MarketplaceWarehouseSortBy
{
    Name = 0,
    Kind = 1,
    SyncedAt = 2,
}

public enum MarketplaceCardSortBy
{
    Name = 0,
    OfferId = 1,
    Price = 2,
    SyncedAt = 3,
}

/// <summary>Mapping-state filter for the cards tab.</summary>
public enum MarketplaceCardMappingState
{
    All = 0,
    Unmapped = 1,
    Mapped = 2,
    ArchivedItem = 3,
}
