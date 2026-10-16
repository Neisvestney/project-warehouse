using System.ComponentModel.DataAnnotations.Schema;
using EntityFrameworkCore.Projectables;
using ProjectWarehouse.Server.Infrastructure;

namespace ProjectWarehouse.Server.Domain;

public class MarketplaceCard : IHasIdentity
{
    public Guid Id { get; set; }

    public Guid MarketplaceAccountId { get; set; }
    public MarketplaceAccount MarketplaceAccount { get; set; } = null!;

    /// <summary>Marketplace product id. Always a string, even where the marketplace uses int64.</summary>
    public string ExternalId { get; set; } = null!;

    public string? Sku { get; set; }
    public string OfferId { get; set; } = null!;
    public string Name { get; set; } = null!;

    [Column(TypeName = "jsonb")] public IList<string> Barcodes { get; set; } = [];

    public string? PrimaryImageUrl { get; set; }
    public decimal? Price { get; set; }
    public string? CurrencyCode { get; set; }
    public bool IsArchived { get; set; }

    /// <summary>Archived on the WMS side even though the marketplace still lists it — sync never touches this.</summary>
    public bool IsMarkedArchived { get; set; }

    [Projectable]
    public bool EffectiveIsArchived => IsArchived || IsMarkedArchived;

    /// <summary>Mapping to the catalog. Sync never resets it — the mapping outlives the card data.</summary>
    public Guid? CatalogItemId { get; set; }
    public CatalogItem? CatalogItem { get; set; }

    public MarketplaceMappingSource? MappingSource { get; set; }
    public DateTime? MappedAt { get; set; }

    public DateTime SyncedAt { get; set; }

    [Projectable]
    public string SearchString => Name + " " + OfferId + " " + ExternalId + " " + (Sku ?? "");

    /// <summary>The catalog item was archived after the mapping was made — the mapping is kept, but flagged.</summary>
    [Projectable]
    public bool IsMappedToArchivedItem => CatalogItem != null && CatalogItem.IsArchived;
}
