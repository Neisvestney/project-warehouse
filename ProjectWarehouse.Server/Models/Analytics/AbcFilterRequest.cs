namespace ProjectWarehouse.Server.Models.Analytics;

/// <summary>
/// The class filters and the search narrow the table and the timeline rows only; tiles, matrix and Pareto
/// always cover the whole analysis.
/// </summary>
public class AbcFilterRequest : AnalyticsFilterRequest
{
    public AnalyticsAbcBasis Basis { get; init; } = AnalyticsAbcBasis.Units;

    /// <summary>Currency of a money basis; null takes the one with the most sale lines.</summary>
    public string? CurrencyCode { get; init; }

    /// <summary>
    /// Starts an item's XYZ series at its first sale ever over the selected channels, so the weeks before it
    /// was launched do not count as zero demand.
    /// </summary>
    public bool XyzFromFirstSale { get; init; } = true;

    public AbcClass? AbcClass { get; init; }

    public XyzClass? XyzClass { get; init; }

    public string? SearchString { get; init; }
}
