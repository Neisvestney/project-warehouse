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

    /// <summary>IANA identifier the warehouse's days are cut by. Null falls back down the resolver chain.</summary>
    public string? TimeZoneId { get; init; }

    // Carried on the read DTO so the changelog diffs them like any other warehouse field, whichever
    // endpoint wrote them. Only TimeZoneId is also writable through UpdateWarehouseRequest.
    public int? StockWarningDays { get; init; }
    public int? ConsumptionWindowDays { get; init; }
    public bool UseWeightedConsumption { get; init; }
    public IReadOnlyList<StoragePlaceDto> StoragePlaces { get; init; } = [];
    public IReadOnlyList<WarehouseLayoutElementDto> LayoutObjects { get; init; } = [];
    public IReadOnlyList<MarketplaceAccountShortSummaryDto> MarketplaceAccounts { get; init; } = [];
    public int TotalItemsCount { get; init; }
}