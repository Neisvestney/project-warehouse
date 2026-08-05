namespace ProjectWarehouse.Server.Models.Integrations;

public enum MarketplaceAccountSortBy
{
    Name,
    CreatedAt,
    LastSyncAt,
}

public enum MarketplaceWarehouseSortBy
{
    Name,
    Kind,
    SyncedAt,
}

public enum MarketplaceCardSortBy
{
    Name,
    OfferId,
    Price,
    SyncedAt,
}

/// <summary>Mapping-state filter for the cards tab.</summary>
public enum MarketplaceCardMappingState
{
    All,
    Unmapped,
    Mapped,
    ArchivedItem,
}
