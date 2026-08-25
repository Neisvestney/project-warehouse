namespace ProjectWarehouse.Server.Models.Statistics;

public class StockMovementDailyPointDto : StockMovementTotalsDto
{
    public DateOnly Date { get; init; }
}

/// <summary><see cref="Items"/> covers every day of the range, including the empty ones — a chart
/// should not have to reconstruct the gaps.</summary>
public class StockMovementDailySeriesDto
{
    public DateOnly From { get; init; }
    public DateOnly To { get; init; }

    /// <summary>IANA zone the days were cut in — label the axis with it, it is not always the caller's own.</summary>
    public string TimeZoneId { get; init; } = null!;

    public IReadOnlyList<StockMovementDailyPointDto> Items { get; init; } = [];
    public StockMovementTotalsDto Totals { get; init; } = new();
}
