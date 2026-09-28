using ProjectWarehouse.Server.Domain;

namespace ProjectWarehouse.Server.Models.Analytics;

/// <summary>What the accrual journal credited per interval, per shop; the same money as «Начислено».</summary>
public class PayoutsTimeseriesDto
{
    /// <summary>The requested first day, or over all time the earliest journal day of the selected shops.</summary>
    public DateOnly From { get; init; }

    public DateOnly To { get; init; }
    public string TimeZoneId { get; init; } = null!;

    /// <summary>The step applied — the requested one, or the one picked by the period length.</summary>
    public AnalyticsStep Step { get; init; }

    public List<AnalyticsIntervalDto> Intervals { get; init; } = [];

    /// <summary>One per currency of the period's journal lines; empty when there are none.</summary>
    public List<PayoutsCurrencySeriesDto> Currencies { get; init; } = [];
}

public class PayoutsCurrencySeriesDto
{
    public string CurrencyCode { get; init; } = null!;

    /// <summary>One per selected shop, by name, zeros included.</summary>
    public List<PayoutsSeriesDto> Series { get; init; } = [];
}

public class PayoutsSeriesDto
{
    public Guid MarketplaceAccountId { get; init; }
    public MarketplaceType MarketplaceType { get; init; }
    public string Name { get; init; } = null!;

    /// <summary>Σ journal amounts per interval, shop-wide lines included; null for a future interval.</summary>
    public List<decimal?> Values { get; init; } = [];

    public decimal Total { get; init; }
}
