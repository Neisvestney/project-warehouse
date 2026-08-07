using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Infrastructure;

namespace ProjectWarehouse.Server.Models.Integrations;

public class MarketplaceSyncRunDto : IHasIdentity
{
    public Guid Id { get; init; }
    public Guid MarketplaceAccountId { get; init; }
    public MarketplaceSyncScope Scope { get; init; }
    public MarketplaceSyncStatus Status { get; init; }

    public DateTime StartedAt { get; init; }
    public DateTime? FinishedAt { get; init; }

    public Guid? TriggeredById { get; init; }
    public string? TriggeredByName { get; init; }

    public int WarehousesProcessed { get; init; }
    public int CardsProcessed { get; init; }
    public int CardsCreated { get; init; }
    public int CardsUpdated { get; init; }
    public int CardsArchived { get; init; }
    public int AutoMapped { get; init; }

    public int OrdersProcessed { get; init; }
    public int OrdersCreated { get; init; }
    public int OrdersUpdated { get; init; }
    public int OrdersSkipped { get; init; }

    /// <summary>Capped at 100; <see cref="OrdersSkipped"/> is the true total. Empty, never null.</summary>
    public IReadOnlyList<SkippedOrderInfo> SkippedOrders { get; init; } = [];

    public AppFieldError? Error { get; init; }
}
