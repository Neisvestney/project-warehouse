using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Infrastructure;

namespace ProjectWarehouse.Server.Models.Integrations;

public class MarketplaceCardDto : IHasIdentity
{
    public Guid Id { get; init; }
    public Guid MarketplaceAccountId { get; init; }
    public string ExternalId { get; init; } = null!;
    public string? Sku { get; init; }
    public string OfferId { get; init; } = null!;
    public string Name { get; init; } = null!;
    public IReadOnlyList<string> Barcodes { get; init; } = [];
    public string? PrimaryImageUrl { get; init; }
    public decimal? Price { get; init; }
    public string? CurrencyCode { get; init; }
    public bool IsArchived { get; init; }

    public Guid? CatalogItemId { get; init; }
    public string? CatalogItemFullName { get; init; }
    public string? CatalogItemArticle { get; init; }
    public MarketplaceMappingSource? MappingSource { get; init; }
    public DateTime? MappedAt { get; init; }

    /// <summary>The mapping survived the catalog item being archived — shown as a chip in the list.</summary>
    public bool IsMappedToArchivedItem { get; init; }

    public DateTime SyncedAt { get; init; }
}
