using System.ComponentModel.DataAnnotations.Schema;
using EntityFrameworkCore.Projectables;
using ProjectWarehouse.Server.Infrastructure;
using ProjectWarehouse.Server.Models;

namespace ProjectWarehouse.Server.Domain;

public class MarketplaceAccount : IHasIdentity
{
    public Guid Id { get; set; }
    public MarketplaceType Type { get; set; }

    /// <summary>Shop name as the marketplace reports it. Overwritten by every sync, never entered by hand —
    /// until the first one lands it holds a placeholder built from the marketplace and the key mask.</summary>
    public string Name { get; set; } = null!;

    public bool IsActive { get; set; } = true;

    // Seller identity, filled by sync alongside Name. All nullable: a self-employed seller has no OGRN.
    public string? CompanyLegalName { get; set; }
    public string? Inn { get; set; }
    public string? Ogrn { get; set; }
    public string? OwnershipForm { get; set; }

    /// <summary>Ozon Client-Id. Left null for providers that authenticate with a token alone.</summary>
    public string? ExternalClientId { get; set; }

    /// <summary>Api-Key ciphertext. Only IMarketplaceCredentialProtector may open it.</summary>
    public string ApiKeyProtected { get; set; } = null!;

    public string ApiKeyLast4 { get; set; } = null!;
    public DateTime? ApiKeyUpdatedAt { get; set; }

    public int SyncIntervalMinutes { get; set; }
    public DateTime? LastSyncAt { get; set; }
    public MarketplaceSyncStatus? LastSyncStatus { get; set; }

    // ErrorCode lands in jsonb as a number — Npgsql serializes it, not the MVC options that stringify enums
    [Column(TypeName = "jsonb")] public AppFieldError? LastSyncError { get; set; }

    public DateTime CreatedAt { get; set; }
    public Guid? CreatedById { get; set; }
    public ApplicationUser? CreatedBy { get; set; }

    public ICollection<MarketplaceWarehouse> Warehouses { get; set; } = [];
    public ICollection<MarketplaceCard> Cards { get; set; } = [];
    public ICollection<MarketplaceSyncRun> SyncRuns { get; set; } = [];

    /// <summary>Imported postings. Their presence blocks deleting the account.</summary>
    public ICollection<MarketplaceOrder> Orders { get; set; } = [];

    [Projectable]
    public string SearchString =>
        Name + " " + (ExternalClientId ?? "") + " " + (CompanyLegalName ?? "") + " " + (Inn ?? "");
}
