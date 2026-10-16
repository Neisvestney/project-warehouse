using ProjectWarehouse.Server.Infrastructure;

namespace ProjectWarehouse.Server.Domain;

public class MarketplaceWarehouse : IHasIdentity
{
    public Guid Id { get; set; }

    public Guid MarketplaceAccountId { get; set; }
    public MarketplaceAccount MarketplaceAccount { get; set; } = null!;

    public string ExternalId { get; set; } = null!;
    public string Name { get; set; } = null!;
    public MarketplaceWarehouseKind Kind { get; set; }
    public MarketplaceWarehouseStatus Status { get; set; }

    /// <summary>Raw marketplace status kept for diagnostics — <see cref="Status"/> is what the UI acts on.</summary>
    public string? ExternalStatus { get; set; }
    public string? Address { get; set; }

    /// <summary>Set when the warehouse stopped appearing in the marketplace listing. Never deleted — a mapping may point at it.</summary>
    public bool IsArchived { get; set; }

    /// <summary>Mapping to a WMS warehouse. Administrator-owned: sync never touches it.</summary>
    public Guid? WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }

    public DateTime SyncedAt { get; set; }
}
