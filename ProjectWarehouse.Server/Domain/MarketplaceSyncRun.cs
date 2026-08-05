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

    // ErrorCode lands in jsonb as a number — Npgsql serializes it, not the MVC options that stringify enums
    [Column(TypeName = "jsonb")] public AppFieldError? Error { get; set; }
}
