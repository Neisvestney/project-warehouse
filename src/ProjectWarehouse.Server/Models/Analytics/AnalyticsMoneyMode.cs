namespace ProjectWarehouse.Server.Models.Analytics;

public enum AnalyticsMoneyMode
{
    /// <summary>Σ Price × Quantity over Delivering and Delivered sales — the whole period, before commission.</summary>
    Price = 0,

    /// <summary>Accrual journal net of the accrued sales — real money, incomplete for a fresh period.</summary>
    Payout = 1,
}
