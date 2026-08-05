using ProjectWarehouse.Server.Infrastructure;
using ProjectWarehouse.Server.Models.Integrations;

namespace ProjectWarehouse.Server.Models.Warehouses;

public class WarehouseDto : IHasIdentity
{
    public Guid Id { get; init; }
    public string Name { get; init; } = null!;
    public decimal Width { get; init; }
    public decimal Height { get; init; }
    public Guid? DefaultStoragePlaceNodeId { get; init; }
    public IReadOnlyList<StoragePlaceDto> StoragePlaces { get; init; } = [];
    public IReadOnlyList<WarehouseLayoutElementDto> LayoutObjects { get; init; } = [];
    public IReadOnlyList<MarketplaceAccountShortSummaryDto> MarketplaceAccounts { get; init; } = [];
    public int TotalItemsCount { get; init; }
}