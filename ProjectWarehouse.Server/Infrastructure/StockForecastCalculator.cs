using ProjectWarehouse.Server.Models.Forecast;

namespace ProjectWarehouse.Server.Infrastructure;

/// <summary>
/// The whole forecast arithmetic, with no database behind it. The service owns permissions, selection
/// and assembly; this owns the number. It is the only piece worth covering with tests directly and the
/// only one that survives a change of data source.
/// </summary>
public static class StockForecastCalculator
{
    public const int DefaultWarningDays = 14;
    public const int DefaultWindowDays = 30;

    public const int MaxWarningDays = 3650;
    public const int MinWindowDays = 1;

    /// <summary>Matches the statistics endpoints and keeps a request from scanning the whole journal.</summary>
    public const int MaxWindowDays = 366;

    /// <summary>Fresh days weigh more; a third of the window is the period over which weight halves.</summary>
    private const double HalfLifeFraction = 3.0;

    /// <param name="stock">Current on-hand quantity.</param>
    /// <param name="dailyOutQuantities">
    /// Exactly <c>options.WindowDays</c> entries, index 0 being today. Days with no shipment are zeros
    /// and must be present: they are days too, and they are what keeps an item that leaves in one box a
    /// month from reading like a bestseller.
    /// </param>
    /// <param name="options">Window size and half-life weighting configuration.</param>
    /// <param name="warningDays">Threshold in days below which the forecast is flagged as low.</param>
    public static StockForecastResult Calculate(
        int stock,
        IReadOnlyList<int> dailyOutQuantities,
        StockForecastOptions options,
        int warningDays)
    {
        if (dailyOutQuantities.Count != options.WindowDays)
            throw new ArgumentException(
                $"Expected {options.WindowDays} daily quantities, got {dailyOutQuantities.Count}.",
                nameof(dailyOutQuantities));

        var consumedInWindow = dailyOutQuantities.Sum();

        // Status keys off the raw total, not off the rounded rate: a trickle of 0.004/day rounds to
        // 0.00 and would otherwise be reported as "nothing ever moved".
        if (consumedInWindow == 0)
            return new StockForecastResult(0m, 0, null, StockForecastStatus.NoConsumption);

        var daily = options.UseWeightedConsumption
            ? Weighted(dailyOutQuantities, options.WindowDays)
            : (double)consumedInWindow / options.WindowDays;

        var rounded = Math.Round((decimal)daily, 2, MidpointRounding.AwayFromZero);

        if (stock <= 0)
            return new StockForecastResult(rounded, consumedInWindow, 0, StockForecastStatus.OutOfStock);

        // Floored: "lasts 2 days" when it truly lasts 2.9 is a safe error, "3" when it truly lasts 2.1 is not.
        var exact = Math.Floor(stock / daily);
        var daysLeft = exact >= int.MaxValue ? int.MaxValue : (int)exact;

        var status = daysLeft == 0
            ? StockForecastStatus.OutOfStock
            : daysLeft <= warningDays
                ? StockForecastStatus.Warning
                : StockForecastStatus.Ok;

        return new StockForecastResult(rounded, consumedInWindow, daysLeft, status);
    }

    /// <summary>
    /// Whether the position is something to act on. <c>NoConsumption</c> is not: nothing is running out.
    /// </summary>
    public static bool IsWarning(StockForecastStatus status) =>
        status is StockForecastStatus.OutOfStock or StockForecastStatus.Warning;

    /// <summary>Threshold chain: item override → warehouse setting → system constant.</summary>
    public static int ResolveWarningDays(int? itemOverride, int? warehouseSetting) =>
        itemOverride ?? warehouseSetting ?? DefaultWarningDays;

    public static int ResolveWindowDays(int? warehouseSetting) =>
        warehouseSetting ?? DefaultWindowDays;

    /// <summary>
    /// Exponentially decaying weights over the whole window, empty days included — they belong in the
    /// denominator exactly as they do in the simple average.
    /// </summary>
    private static double Weighted(IReadOnlyList<int> dailyOutQuantities, int windowDays)
    {
        var halfLife = windowDays / HalfLifeFraction;
        double weightedSum = 0;
        double weightTotal = 0;

        for (var age = 0; age < windowDays; age++)
        {
            var weight = Math.Pow(0.5, age / halfLife);
            weightedSum += dailyOutQuantities[age] * weight;
            weightTotal += weight;
        }

        return weightedSum / weightTotal;
    }
}
