namespace ProjectWarehouse.Server.Models.Analytics;

/// <summary>Query of the payouts page. The period applies to accrued money only; both ends absent mean all time.</summary>
public class PayoutsRequest
{
    /// <summary>Inclusive first day, in the caller's time zone.</summary>
    public DateOnly? From { get; init; }

    /// <summary>Inclusive last day, in the caller's time zone.</summary>
    public DateOnly? To { get; init; }

    /// <inheritdoc cref="AnalyticsFilterRequest.IncludeMarketplaces"/>
    public bool IncludeMarketplaces { get; init; } = true;

    /// <summary>Shops to include. Empty means all.</summary>
    public Guid[]? MarketplaceAccountIds { get; init; }
}
