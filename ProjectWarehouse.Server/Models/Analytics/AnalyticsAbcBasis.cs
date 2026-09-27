namespace ProjectWarehouse.Server.Models.Analytics;

/// <summary>What an item's ABC value is.</summary>
public enum AnalyticsAbcBasis
{
    /// <summary>Σ Quantity over every selected channel, Direct included.</summary>
    Units = 0,

    /// <summary>Σ Price × Quantity of shop sales in one currency.</summary>
    Price = 1,

    /// <summary>Σ Payout of accrued shop lines in one currency.</summary>
    Payout = 2,
}
