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
    /// <param name="stockedDays">
    /// Same indexing as <paramref name="dailyOutQuantities"/>; <c>false</c> days are left out of the average
    /// entirely. Null counts every day. See <see cref="FindStockedDays"/>.
    /// </param>
    public static StockForecastResult Calculate(
        int stock,
        IReadOnlyList<int> dailyOutQuantities,
        StockForecastOptions options,
        int warningDays,
        IReadOnlyList<bool>? stockedDays = null)
    {
        if (dailyOutQuantities.Count != options.WindowDays)
            throw new ArgumentException(
                $"Expected {options.WindowDays} daily quantities, got {dailyOutQuantities.Count}.",
                nameof(dailyOutQuantities));

        if (stockedDays is not null && stockedDays.Count != options.WindowDays)
            throw new ArgumentException(
                $"Expected {options.WindowDays} stocked-day flags, got {stockedDays.Count}.",
                nameof(stockedDays));

        var consumedInWindow = dailyOutQuantities.Sum();

        // Status keys off the raw total, not off the rounded rate: a trickle of 0.004/day rounds to
        // 0.00 and would otherwise be reported as "nothing ever moved".
        if (consumedInWindow == 0)
            return new StockForecastResult(0m, 0, null, StockForecastStatus.NoConsumption);

        // Never zero here: a day with any Out is stocked by definition.
        var countedDays = stockedDays?.Count(d => d) ?? options.WindowDays;

        var daily = options.UseWeightedConsumption
            ? Weighted(dailyOutQuantities, options.WindowDays, stockedDays)
            : (double)consumedInWindow / countedDays;

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
    /// Days since the item's stock last hit zero, walking back from today through
    /// <paramref name="dailyNetChanges"/> (index 0 = today, signed: in - out for that day).
    /// Returns <c>0</c> when <paramref name="stock"/> is already zero, a positive age when an earlier day in
    /// the window balanced to zero, or <c>null</c> when no day in the window did. Informational only — the
    /// average leaves out out-of-stock days through <see cref="FindStockedDays"/> instead.
    /// </summary>
    public static int? FindLastZeroStockAge(int stock, IReadOnlyList<int> dailyNetChanges)
    {
        if (stock == 0) return 0;

        var balance = stock;
        for (var age = 0; age < dailyNetChanges.Count; age++)
        {
            balance -= dailyNetChanges[age];
            if (balance == 0) return age + 1;
        }

        return null;
    }

    /// <summary>
    /// Which days of the window the item was available on, reconstructing the end-of-day balance backward
    /// from <paramref name="stock"/> through <paramref name="dailyNetChanges"/> (index 0 = today). A day is
    /// left out only when it opened and closed at zero with nothing shipped: a day that sold out at noon or
    /// was restocked and sold from still had goods to consume. Exact zero, not "≤ 0": a negative
    /// reconstruction means the journal misses an earlier receipt, and the goods were most likely there.
    /// </summary>
    public static bool[] FindStockedDays(
        int stock, IReadOnlyList<int> dailyNetChanges, IReadOnlyList<int> dailyOutQuantities)
    {
        if (dailyOutQuantities.Count != dailyNetChanges.Count)
            throw new ArgumentException(
                $"Expected {dailyNetChanges.Count} daily quantities, got {dailyOutQuantities.Count}.",
                nameof(dailyOutQuantities));

        var stocked = new bool[dailyNetChanges.Count];
        var closing = stock;

        for (var age = 0; age < dailyNetChanges.Count; age++)
        {
            var opening = closing - dailyNetChanges[age];
            stocked[age] = opening != 0 || closing != 0 || dailyOutQuantities[age] > 0;
            closing = opening;
        }

        return stocked;
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
    /// denominator exactly as they do in the simple average. Out-of-stock days are skipped, keeping the
    /// age-based weight of the rest.
    /// </summary>
    private static double Weighted(
        IReadOnlyList<int> dailyOutQuantities, int windowDays, IReadOnlyList<bool>? stockedDays)
    {
        var halfLife = windowDays / HalfLifeFraction;
        double weightedSum = 0;
        double weightTotal = 0;

        for (var age = 0; age < windowDays; age++)
        {
            if (stockedDays is not null && !stockedDays[age]) continue;

            var weight = Math.Pow(0.5, age / halfLife);
            weightedSum += dailyOutQuantities[age] * weight;
            weightTotal += weight;
        }

        return weightedSum / weightTotal;
    }
}
