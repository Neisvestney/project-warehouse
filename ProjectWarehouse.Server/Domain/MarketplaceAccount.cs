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

    /// <summary>Linked by <see cref="Inn"/> on every sync unless <see cref="IsOrganizationLinkedManually"/>.</summary>
    public Guid? OrganizationId { get; set; }
    public Organization? Organization { get; set; }

    /// <summary>Set by an operator's choice; sync leaves such a link alone.</summary>
    public bool IsOrganizationLinkedManually { get; set; }

    /// <summary>Ozon Client-Id. Left null for providers that authenticate with a token alone.</summary>
    public string? ExternalClientId { get; set; }

    /// <summary>Api-Key ciphertext. Only IMarketplaceCredentialProtector may open it.</summary>
    public string ApiKeyProtected { get; set; } = null!;

    public string ApiKeyLast4 { get; set; } = null!;
    public DateTime? ApiKeyUpdatedAt { get; set; }

    public int SyncIntervalMinutes { get; set; }
    public DateTime? LastSyncAt { get; set; }
    public MarketplaceSyncStatus? LastSyncStatus { get; set; }

    /// <summary>
    /// Up to when marketplace-fulfilled postings have been imported. Moves only after the FBO import of a
    /// run finished, so a failed run leaves no hole; null until the first one. Not <see cref="LastSyncAt"/>,
    /// which any scope moves — a cards-only run would otherwise skip a day of postings.
    /// </summary>
    public DateTime? FboPostingsSyncedAt { get; set; }

    /// <summary>
    /// Up to when status changes of FBS postings WMS never imported have been read. Same rules as
    /// <see cref="FboPostingsSyncedAt"/>; the history import never moves it.
    /// </summary>
    public DateTime? FbsPostingsSyncedAt { get; set; }

    /// <summary>
    /// Up to when the stream of return status changes has been read. Same rules as
    /// <see cref="FboPostingsSyncedAt"/>; the history import never moves it.
    /// </summary>
    public DateTime? ReturnsSyncedAt { get; set; }

    /// <summary>
    /// When accruals were last read. Same rules as <see cref="FboPostingsSyncedAt"/>; the history import never
    /// moves it. A run starts the day before it, so a quiet account picks up the days it missed.
    /// </summary>
    public DateTime? AccrualsSyncedAt { get; set; }

    /// <summary>
    /// When accruals were last re-read over the whole overlap. The marketplace posts reversals and return
    /// logistics days after the sale, but re-reading two weeks on every run would cost dozens of throttled calls,
    /// so the long pass runs once a day and the runs in between read only the latest days.
    /// </summary>
    public DateTime? AccrualsFullPassAt { get; set; }

    /// <summary>
    /// When the latest buyouts were last read in full. Kept apart from <see cref="AccrualsSyncedAt"/>: the buyout
    /// report runs out of quota for hours, and a refused run must not let the journal's mark carry its days away.
    /// </summary>
    public DateTime? BuyoutsSyncedAt { get; set; }

    /// <summary>
    /// First day from which buyouts are read without a gap up to <see cref="BuyoutsSyncedAt"/>; null before the
    /// first window. Every run reads one more window below it until it reaches the journal's first day.
    /// </summary>
    public DateOnly? BuyoutsLoadedFrom { get; set; }

    /// <summary>
    /// Until when the buyout report is not asked after it answered 429; null or past — ask as usual. Asking
    /// through the refusal only keeps it going.
    /// </summary>
    public DateTime? BuyoutsPausedUntil { get; set; }

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
    public int UnmappedCardCount => Cards.Count(c => c.CatalogItemId == null && !c.EffectiveIsArchived);

    [Projectable]
    public string SearchString =>
        Name + " " + (ExternalClientId ?? "") + " " + (CompanyLegalName ?? "") + " " + (Inn ?? "");
}
