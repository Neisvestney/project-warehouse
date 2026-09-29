using ProjectWarehouse.Server.Domain;

namespace ProjectWarehouse.Server.Models.Analytics;

public class PayoutsDto
{
    /// <summary>Period of the accrued bucket; the debt buckets ignore it.</summary>
    public DateOnly From { get; init; }

    public DateOnly To { get; init; }
    public DateOnly Today { get; init; }
    public string TimeZoneId { get; init; } = null!;
    public PayoutsAppliedSettingsDto Settings { get; init; } = null!;

    /// <summary>One per selected shop, by name.</summary>
    public List<PayoutsRowDto> Rows { get; init; } = [];

    /// <summary>Every shop together, per currency; estimates are the sums of each shop's own estimate.</summary>
    public List<PayoutsMoneyDto> Totals { get; init; } = [];
}

public class PayoutsAppliedSettingsDto
{
    public int PayoutRatioWindowDays { get; init; }
    public IReadOnlyList<int> PayoutAgeBoundaries { get; init; } = [];
    public int PayoutOverdueDays { get; init; }
    public int PayoutNotAccruedDays { get; init; }
}

public class PayoutsRowDto
{
    public Guid MarketplaceAccountId { get; init; }
    public MarketplaceType MarketplaceType { get; init; }
    public string Name { get; init; } = null!;

    /// <summary>
    /// First day of the shop's accrual journal; null when it has none. A delivered posting dated earlier and
    /// never accrued is left out of every bucket.
    /// </summary>
    public DateOnly? CoveredFrom { get; init; }

    /// <summary>Delivered postings dated before <see cref="CoveredFrom"/> with no sale in the journal.</summary>
    public int UncoveredPostings { get; init; }

    public List<PayoutsMoneyDto> Money { get; init; } = [];
}

/// <summary>
/// The buckets of one shop, or of all of them, in one currency. An estimate is Σ Price × Quantity × the shop's
/// payout ratio and is null when the ratio is unknown; posting counts are exact either way.
/// </summary>
public class PayoutsMoneyDto
{
    public string CurrencyCode { get; init; } = null!;

    /// <summary>Journal net over sale price of the shop's recently accrued postings; null on the totals row.</summary>
    public double? PayoutRatio { get; init; }

    public decimal? InTransit { get; init; }
    public int InTransitPostings { get; init; }

    /// <summary><c>payoutAgeBoundaries.Count + 1</c> buckets of <see cref="InTransit"/> by age; empty when it is null.</summary>
    public List<decimal> InTransitByAge { get; init; } = [];

    /// <summary>Part of <see cref="InTransit"/> at least <c>payoutOverdueDays</c> old.</summary>
    public decimal? InTransitOverdue { get; init; }

    public int InTransitOverduePostings { get; init; }

    public decimal? DeliveredNotAccrued { get; init; }
    public int DeliveredNotAccruedPostings { get; init; }

    /// <summary>Delivered at least <c>payoutNotAccruedDays</c> ago with no sale — out of the marketplace's debt.</summary>
    public decimal? NotAccruedByMarketplace { get; init; }

    public int NotAccruedByMarketplacePostings { get; init; }

    /// <summary>Σ journal amounts dated in the period, shop-wide lines included; exact.</summary>
    public decimal Accrued { get; init; }

    /// <summary>Postings with a journal line dated in the period.</summary>
    public int AccruedPostings { get; init; }

    /// <summary>Every journal line dated in the period by category; adds up to <see cref="Accrued"/>.</summary>
    public List<PayoutsCategoryDto> Categories { get; init; } = [];
}

public class PayoutsCategoryDto
{
    public MarketplaceAccrualCategory Category { get; init; }

    /// <summary>A category read from several documents comes as a line per document.</summary>
    public MarketplaceAccrualSource Source { get; init; }

    /// <summary>The lines are tied to postings; otherwise they concern the shop as a whole.</summary>
    public bool ByPosting { get; init; }

    public decimal Amount { get; init; }
}
