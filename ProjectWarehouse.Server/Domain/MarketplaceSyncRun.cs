using System.ComponentModel.DataAnnotations.Schema;
using ProjectWarehouse.Server.Infrastructure;
using ProjectWarehouse.Server.Models;

namespace ProjectWarehouse.Server.Domain;

public class MarketplaceSyncRun : IHasIdentity
{
    public Guid Id { get; set; }

    public Guid MarketplaceAccountId { get; set; }
    public MarketplaceAccount MarketplaceAccount { get; set; } = null!;

    public MarketplaceSyncScope Scope { get; set; }
    public MarketplaceSyncStatus Status { get; set; }

    public DateTime StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }

    /// <summary>Null for scheduled runs.</summary>
    public Guid? TriggeredById { get; set; }
    public ApplicationUser? TriggeredBy { get; set; }

    public int WarehousesProcessed { get; set; }
    public int CardsProcessed { get; set; }
    public int CardsCreated { get; set; }
    public int CardsUpdated { get; set; }
    public int CardsArchived { get; set; }
    public int AutoMapped { get; set; }

    // filled only when Scope is Orders
    public int OrdersProcessed { get; set; }
    public int OrdersCreated { get; set; }
    public int OrdersUpdated { get; set; }
    public int OrdersSkipped { get; set; }

    /// <summary>
    /// First 100 skipped postings with their reason; <see cref="OrdersSkipped"/> holds the true total.
    /// A silent skip is the worst failure mode here — it surfaces at the warehouse when it is too late.
    /// </summary>
    [Column(TypeName = "jsonb")] public IList<SkippedOrderInfo>? SkippedOrders { get; set; }

    // ErrorCode lands in jsonb as a number — Npgsql serializes it, not the MVC options that stringify enums
    [Column(TypeName = "jsonb")] public AppFieldError? Error { get; set; }
}
