using ProjectWarehouse.Server.Domain;

namespace ProjectWarehouse.Server.Models.Analytics;

public class ChannelsSummaryDto
{
    public DateOnly From { get; init; }
    public DateOnly To { get; init; }

    /// <summary>The period of the same length right before the selected one, which Δ compares against.</summary>
    public DateOnly PreviousFrom { get; init; }
    public DateOnly PreviousTo { get; init; }

    public string TimeZoneId { get; init; } = null!;
    public AnalyticsMoneyMode MoneyMode { get; init; }

    public int ReturnsMaturityDays { get; init; }

    /// <summary>The period ends closer to today than the maturity window: return shares are still growing.</summary>
    public bool ReturnsImmature { get; init; }

    public List<ChannelSummaryRowDto> Rows { get; init; } = [];
    public List<ChannelCancellationsDto> Cancellations { get; init; } = [];
    public List<CancelReasonCountDto> TopCancelReasons { get; init; } = [];
}

public class ChannelSummaryRowDto
{
    public AnalyticsChannelKind Kind { get; init; }

    public Guid? MarketplaceAccountId { get; init; }
    public MarketplaceType? MarketplaceType { get; init; }
    public Guid? TagId { get; init; }

    /// <summary>Shop or tag name; null for the whole Direct channel and for its untagged row.</summary>
    public string? Name { get; init; }

    public int Orders { get; init; }
    public int PreviousOrders { get; init; }
    public int Units { get; init; }
    public int Cancellations { get; init; }

    /// <summary>Cancellations / (sales + cancellations); null when there were neither.</summary>
    public double? CancellationRate { get; init; }

    /// <summary>Null for Direct, which has no returns.</summary>
    public int? ReturnedUnits { get; init; }
    public double? ReturnRate { get; init; }

    /// <summary>Share of the channel's units among the selected channels.</summary>
    public double? UnitsShare { get; init; }

    /// <summary>Share of accrued lines among sale lines with a currency; filled in the Payout mode only.</summary>
    public double? PayoutCoverage { get; init; }

    /// <summary>One entry per currency, never summed across them. Empty for Direct.</summary>
    public List<ChannelMoneyDto> Money { get; init; } = [];
}

public class ChannelMoneyDto
{
    public string CurrencyCode { get; init; } = null!;

    /// <summary>In the requested money mode.</summary>
    public decimal Revenue { get; init; }

    /// <summary>Revenue / sales that have money in this currency in this mode.</summary>
    public decimal? AverageCheck { get; init; }

    /// <summary>Share of the shop in this currency's revenue among the selected shops.</summary>
    public double? RevenueShare { get; init; }

    /// <summary>1 − Σ(Price × Q) / Σ(OldPrice × Q), weighted by money; null without an old price.</summary>
    public double? DiscountDepth { get; init; }

    /// <summary>Σ DiscountValue × Q.</summary>
    public decimal DiscountAmount { get; init; }
}

public class ChannelCancellationsDto
{
    public Guid MarketplaceAccountId { get; init; }
    public List<CancellationTypeCountDto> ByType { get; init; } = [];

    /// <summary>Cancelled after the marketplace had already taken the shipment.</summary>
    public int AfterShip { get; init; }
}

public class CancellationTypeCountDto
{
    /// <summary>A posting with no stated initiator counts as Unknown.</summary>
    public MarketplaceCancellationType Type { get; init; }

    public int Count { get; init; }
}

public class CancelReasonCountDto
{
    public string Reason { get; init; } = null!;
    public int Count { get; init; }
}
