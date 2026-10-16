using System.ComponentModel.DataAnnotations;

namespace ProjectWarehouse.Server.Models.Analytics;

/// <summary>Postings of one debt bucket in one currency. Buckets ignore the period, so the request has none.</summary>
public class PayoutsPostingsRequest
{
    [Required]
    public PayoutsBucket? Bucket { get; init; }

    [Required]
    public string CurrencyCode { get; init; } = null!;

    /// <inheritdoc cref="AnalyticsFilterRequest.IncludeMarketplaces"/>
    public bool IncludeMarketplaces { get; init; } = true;

    /// <summary>Shops to include. Empty means all.</summary>
    public Guid[]? MarketplaceAccountIds { get; init; }

    [Range(1, int.MaxValue)]
    public int Page { get; init; } = 1;

    [Range(1, 200)]
    public int PageSize { get; init; } = 50;
}
