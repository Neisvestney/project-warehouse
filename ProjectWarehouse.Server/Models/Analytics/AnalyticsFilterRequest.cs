using System.ComponentModel.DataAnnotations;

namespace ProjectWarehouse.Server.Models.Analytics;

/// <summary>Period and channels shared by every analytics report.</summary>
public class AnalyticsFilterRequest
{
    /// <summary>Inclusive first day, in the caller's time zone.</summary>
    [Required]
    public DateOnly? From { get; init; }

    /// <summary>Inclusive last day, in the caller's time zone.</summary>
    [Required]
    public DateOnly? To { get; init; }

    /// <summary>
    /// False leaves every shop out, so the Direct channel alone can be asked for — an empty
    /// <see cref="MarketplaceAccountIds"/> means all shops and cannot say "none".
    /// </summary>
    public bool IncludeMarketplaces { get; init; } = true;

    /// <summary>Shops to include. Empty means all.</summary>
    public Guid[]? MarketplaceAccountIds { get; init; }

    public bool IncludeDirect { get; init; } = true;

    /// <summary>Narrows the Direct channel to orders carrying any of these tags. Empty means all.</summary>
    public Guid[]? DirectTagIds { get; init; }
}
