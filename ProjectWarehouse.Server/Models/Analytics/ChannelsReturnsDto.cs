using ProjectWarehouse.Server.Domain;

namespace ProjectWarehouse.Server.Models.Analytics;

/// <summary>
/// Everything but <see cref="Series"/> is tied to the sales of the period (the order date), so the share does
/// not depend on when the buyer brought the item back. <see cref="Series"/> is tied to the return date instead:
/// how much came back in each interval.
/// </summary>
public class ChannelsReturnsDto
{
    public DateOnly From { get; init; }
    public DateOnly To { get; init; }
    public string TimeZoneId { get; init; } = null!;
    public AnalyticsMoneyMode MoneyMode { get; init; }

    public int ReturnsMaturityDays { get; init; }
    public bool ReturnsImmature { get; init; }

    public List<AccountReturnsDto> Accounts { get; init; } = [];

    public AnalyticsStep Step { get; init; }
    public List<AnalyticsIntervalDto> Intervals { get; init; } = [];

    /// <summary>Returned units by return date, one per shop.</summary>
    public List<ChannelSeriesDto> Series { get; init; } = [];

    public List<ReturnReasonUnitsDto> TopReasons { get; init; } = [];
    public List<CompensationCountDto> Compensations { get; init; } = [];
}

public class AccountReturnsDto
{
    public Guid MarketplaceAccountId { get; init; }
    public MarketplaceType MarketplaceType { get; init; }
    public string Name { get; init; } = null!;

    public int SoldUnits { get; init; }
    public int ReturnedUnits { get; init; }
    public double? ReturnRate { get; init; }

    public List<ReturnKindUnitsDto> ByKind { get; init; } = [];

    /// <summary>Σ Price × Quantity of the returns, per currency; empty in the Payout mode.</summary>
    public List<MoneyAmountDto> Money { get; init; } = [];
}

public class ReturnKindUnitsDto
{
    public MarketplaceReturnKind Kind { get; init; }
    public int Units { get; init; }
}

public class ReturnReasonUnitsDto
{
    public string Reason { get; init; } = null!;
    public int Units { get; init; }
}

public class CompensationCountDto
{
    public MarketplaceReturnCompensationStatus Status { get; init; }
    public int Count { get; init; }
}

public class MoneyAmountDto
{
    public string CurrencyCode { get; init; } = null!;
    public decimal Amount { get; init; }
}
