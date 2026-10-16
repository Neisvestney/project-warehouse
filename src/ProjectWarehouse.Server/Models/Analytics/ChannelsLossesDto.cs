namespace ProjectWarehouse.Server.Models.Analytics;

/// <summary>
/// Sales of the selected channels per interval next to what was lost of them. Everything is tied to the order
/// date: a return belongs to the interval its sale was made in, whenever the item came back.
/// </summary>
public class ChannelsLossesDto
{
    public DateOnly From { get; init; }
    public DateOnly To { get; init; }
    public string TimeZoneId { get; init; } = null!;

    public AnalyticsStep Step { get; init; }
    public AnalyticsMeasure Measure { get; init; }
    public int ReturnsMaturityDays { get; init; }

    /// <summary>One per interval of the step, clamped to the period.</summary>
    public List<LossesPointDto> Points { get; init; } = [];
}

/// <summary>Counts are null for an interval that starts after today; the shop ones also when no shop is selected.</summary>
public class LossesPointDto
{
    public AnalyticsIntervalDto Interval { get; init; } = null!;

    /// <summary>The interval ends closer to today than the maturity window: its returns are still coming in.</summary>
    public bool ReturnsImmature { get; init; }

    /// <summary>Sale orders or units of every selected channel, by the requested measure.</summary>
    public int? Sales { get; init; }

    /// <summary>Sale orders of every selected channel.</summary>
    public int? SaleOrders { get; init; }

    /// <summary>Cancelled orders of every selected channel; the share is taken of sales plus cancellations.</summary>
    public int? Cancellations { get; init; }

    /// <summary>Units sold by the shops alone — Direct has no returns, so the return share is taken of these.</summary>
    public int? ShopUnits { get; init; }

    /// <summary>Returned units of the sales dated in the interval.</summary>
    public int? ReturnedUnits { get; init; }
}
