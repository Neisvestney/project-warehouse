using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Infrastructure;

namespace ProjectWarehouse.Server.Models.Integrations;

public class MarketplaceWarehouseDto : IHasIdentity
{
    public Guid Id { get; init; }
    public Guid MarketplaceAccountId { get; init; }
    public string ExternalId { get; init; } = null!;
    public string Name { get; init; } = null!;
    public MarketplaceWarehouseKind Kind { get; init; }
    public MarketplaceWarehouseStatus Status { get; init; }
    public string? ExternalStatus { get; init; }
    public string? Address { get; init; }
    public bool IsArchived { get; init; }

    public Guid? WarehouseId { get; init; }
    public string? WarehouseName { get; init; }

    public DateTime SyncedAt { get; init; }
}
