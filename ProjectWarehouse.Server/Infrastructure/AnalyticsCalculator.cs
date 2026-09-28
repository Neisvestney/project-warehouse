using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Models.Analytics;

namespace ProjectWarehouse.Server.Infrastructure;

/// <summary>
/// The analytics arithmetic that needs no database: ABC and XYZ classes, interval slicing, age buckets and
/// weekday averages. Parameters come in explicitly rather than being read from settings, so the same numbers
/// can be computed for any configuration and tested directly.
/// </summary>
public static class AnalyticsCalculator
{
    public const decimal DefaultAbcBoundaryA = 80m;
    public const decimal DefaultAbcBoundaryB = 95m;
    public const decimal DefaultXyzBoundaryX = 10m;
    public const decimal DefaultXyzBoundaryY = 25m;
    public const AnalyticsXyzStep DefaultXyzStep = AnalyticsXyzStep.Week;
    public const int DefaultXyzMinIntervals = 4;
    public const int DefaultPayoutRatioWindowDays = 30;
    public static readonly IReadOnlyList<int> DefaultPayoutAgeBoundaries = [7, 14, 30];
    public const int DefaultPayoutOverdueDays = 30;
    public const int DefaultPayoutNotAccruedDays = 30;
    public const int DefaultReturnsMaturityDays = 21;

    public const double MinAbcBoundaryA = 1;
    public const double MaxAbcBoundaryA = 99;
    public const double MinAbcBoundaryB = 2;
    public const double MaxAbcBoundaryB = 99.9;
    public const double MinXyzBoundaryX = 1;
    public const double MaxXyzBoundaryX = 999;
    public const double MinXyzBoundaryY = 2;
    public const double MaxXyzBoundaryY = 1000;
    public const int MinXyzMinIntervals = 2;
    public const int MaxXyzMinIntervals = 52;
    public const int MinPayoutRatioWindowDays = 7;
    public const int MaxPayoutRatioWindowDays = 365;
    public const int MaxPayoutAgeBoundaryCount = 5;
    public const int MinPayoutAgeBoundary = 1;
    public const int MaxPayoutAgeBoundary = 365;
    public const int MinPayoutOverdueDays = 1;
    public const int MaxPayoutOverdueDays = 365;
    public const int MinPayoutNotAccruedDays = 1;
    public const int MaxPayoutNotAccruedDays = 365;
    public const int MinReturnsMaturityDays = 1;
    public const int MaxReturnsMaturityDays = 120;

    /// <summary>A yearly chart fits; "all time" over raw orders without aggregates does not.</summary>
    public const int MaxPeriodDays = 366;

    public const int TimelineMonths = 12;

    /// <summary>13 weeks: a quarter's worth of weekly XYZ intervals behind every month.</summary>
    public const int TimelineWindowDays = 91;

    public static AnalyticsOptions Defaults { get; } = Resolve(null);

    public static AnalyticsOptions Resolve(AnalyticsSettings? settings) => new()
    {
        AbcBoundaryA = settings?.AbcBoundaryA ?? DefaultAbcBoundaryA,
        AbcBoundaryB = settings?.AbcBoundaryB ?? DefaultAbcBoundaryB,
        XyzBoundaryX = settings?.XyzBoundaryX ?? DefaultXyzBoundaryX,
        XyzBoundaryY = settings?.XyzBoundaryY ?? DefaultXyzBoundaryY,
        XyzStep = settings?.XyzStep ?? DefaultXyzStep,
        XyzMinIntervals = settings?.XyzMinIntervals ?? DefaultXyzMinIntervals,
        PayoutRatioWindowDays = settings?.PayoutRatioWindowDays ?? DefaultPayoutRatioWindowDays,
        PayoutAgeBoundaries = settings?.PayoutAgeBoundaries ?? DefaultPayoutAgeBoundaries,
        PayoutOverdueDays = settings?.PayoutOverdueDays ?? DefaultPayoutOverdueDays,
        PayoutNotAccruedDays = settings?.PayoutNotAccruedDays ?? DefaultPayoutNotAccruedDays,
        ReturnsMaturityDays = settings?.ReturnsMaturityDays ?? DefaultReturnsMaturityDays,
    };

    /// <summary>The period of the same length that ends the day before <paramref name="from"/>.</summary>
    public static (DateOnly From, DateOnly To) PreviousPeriod(DateOnly from, DateOnly to)
    {
        var days = to.DayNumber - from.DayNumber + 1;
        return (from.AddDays(-days), from.AddDays(-1));
    }

    /// <summary>
    /// Ranks positive values descending (ties by id) and classes each by the cumulative share of the
    /// positions <b>before</b> it: the position that crosses boundary A is still A, and the first one always is.
    /// </summary>
    /// <param name="values">Item values; zero and negative ones are left out.</param>
    /// <param name="boundaryA">Percent.</param>
    /// <param name="boundaryB">Percent.</param>
    public static IReadOnlyList<AbcRankedItem<TKey>> RankAbc<TKey>(
        IEnumerable<(TKey Id, decimal Value)> values, decimal boundaryA, decimal boundaryB)
        where TKey : IComparable<TKey>
    {
        var ranked = values
            .Where(v => v.Value > 0)
            .OrderByDescending(v => v.Value)
            .ThenBy(v => v.Id)
            .ToList();

        var total = ranked.Sum(v => v.Value);
        var result = new List<AbcRankedItem<TKey>>(ranked.Count);
        var preceding = 0m;

        foreach (var (id, value) in ranked)
        {
            var abcClass = ClassifyAbc(preceding / total * 100m, boundaryA, boundaryB);

            preceding += value;
            result.Add(new AbcRankedItem<TKey>(id, value, (double)(value / total), (double)(preceding / total), abcClass));
        }

        return result;
    }

    /// <summary>
    /// The class <paramref name="value"/> would take among <paramref name="ranked"/> by the same preceding-share
    /// rule, without joining the ranking — for a total of ranked items, such as a variation's members.
    /// </summary>
    public static AbcClass? ClassifyAbcAmong<TKey>(
        decimal value, IReadOnlyList<AbcRankedItem<TKey>> ranked, decimal boundaryA, decimal boundaryB)
    {
        if (value <= 0) return null;

        var total = ranked.Sum(r => r.Value);
        if (total == 0) return AbcClass.A;

        var preceding = ranked.Where(r => r.Value > value).Sum(r => r.Value);
        return ClassifyAbc(preceding / total * 100m, boundaryA, boundaryB);
    }

    private static AbcClass ClassifyAbc(decimal precedingPercent, decimal boundaryA, decimal boundaryB) =>
        precedingPercent < boundaryA ? AbcClass.A
        : precedingPercent < boundaryB ? AbcClass.B
        : AbcClass.C;

    /// <summary>
    /// σ / μ with the population deviation. Null for an empty series and for μ = 0 — sales that fell only
    /// into the discarded partial intervals.
    /// </summary>
    public static double? CoefficientOfVariation(IReadOnlyList<int> series)
    {
        if (series.Count == 0) return null;

        var mean = series.Average();
        if (mean == 0) return null;

        var variance = series.Sum(v => (v - mean) * (v - mean)) / series.Count;
        return Math.Sqrt(variance) / mean;
    }

    /// <param name="cv">As a fraction, the way <see cref="CoefficientOfVariation"/> returns it.</param>
    /// <param name="boundaryX">Percent.</param>
    /// <param name="boundaryY">Percent.</param>
    public static XyzClass? ClassifyXyz(double? cv, decimal boundaryX, decimal boundaryY)
    {
        if (cv is not { } value) return null;

        var percent = value * 100;
        return percent <= (double)boundaryX ? XyzClass.X
            : percent <= (double)boundaryY ? XyzClass.Y
            : XyzClass.Z;
    }

    /// <summary>
    /// Cuts the period into consecutive intervals of <paramref name="step"/>. The first and last are clamped
    /// to the period and flagged partial when their natural bounds stick out of it.
    /// </summary>
    public static IReadOnlyList<AnalyticsInterval> SplitIntervals(DateOnly from, DateOnly to, AnalyticsStep step)
    {
        var result = new List<AnalyticsInterval>();
        var start = StartOf(from, step);

        while (start <= to)
        {
            var next = step switch
            {
                AnalyticsStep.Day => start.AddDays(1),
                AnalyticsStep.Week => start.AddDays(7),
                _ => start.AddMonths(1),
            };
            var end = next.AddDays(-1);

            result.Add(new AnalyticsInterval(
                start < from ? from : start,
                end > to ? to : end,
                start < from || end > to));

            start = next;
        }

        return result;
    }

    /// <summary>
    /// Only the intervals of <paramref name="step"/> that lie entirely inside the period and are over before
    /// <paramref name="today"/>: a week still selling, or one ahead, would add a low value and read as unsteady.
    /// </summary>
    public static IReadOnlyList<AnalyticsInterval> FullIntervals(
        DateOnly from, DateOnly to, AnalyticsXyzStep step, DateOnly today) =>
        SplitIntervals(from, to, step == AnalyticsXyzStep.Week ? AnalyticsStep.Week : AnalyticsStep.Month)
            .Where(i => !i.IsPartial && i.End < today)
            .ToList();

    /// <summary>
    /// The intervals from the one holding <paramref name="firstSale"/> on: weeks before an item existed are not
    /// zero demand, and counting them would class every newcomer as unsteady.
    /// </summary>
    public static IReadOnlyList<AnalyticsInterval> IntervalsSince(
        IReadOnlyList<AnalyticsInterval> intervals, DateOnly firstSale) =>
        intervals.Where(i => i.End >= firstSale).ToList();

    /// <summary>
    /// Sums daily values into their intervals. An interval that starts after <paramref name="today"/> gets
    /// null rather than zero, so a chart line stops instead of diving. Days outside every interval are ignored.
    /// </summary>
    public static List<int?> SumByInterval(
        IReadOnlyList<AnalyticsInterval> intervals, DateOnly today, IEnumerable<(DateOnly Day, int Value)> values)
    {
        var sums = new int[intervals.Count];
        foreach (var (day, value) in values)
        {
            var index = IntervalIndex(intervals, day);
            if (index >= 0) sums[index] += value;
        }

        return intervals.Select((interval, i) => interval.Start > today ? (int?)null : sums[i]).ToList();
    }

    public static List<AnalyticsIntervalDto> ToIntervalDtos(IReadOnlyList<AnalyticsInterval> intervals, DateOnly today) =>
        intervals
            .Select(i => new AnalyticsIntervalDto
            {
                Start = i.Start,
                End = i.End,
                IsPartial = i.IsPartial,
                IsCurrent = i.Start <= today && today <= i.End,
                IsFuture = i.Start > today,
            })
            .ToList();

    /// <summary>The money counterpart of the count overload: same buckets, same null for a future interval.</summary>
    public static List<decimal?> SumByInterval(
        IReadOnlyList<AnalyticsInterval> intervals, DateOnly today, IEnumerable<(DateOnly Day, decimal Value)> values)
    {
        var sums = new decimal[intervals.Count];
        foreach (var (day, value) in values)
        {
            var index = IntervalIndex(intervals, day);
            if (index >= 0) sums[index] += value;
        }

        return intervals.Select((interval, i) => interval.Start > today ? (decimal?)null : sums[i]).ToList();
    }

    /// <summary>
    /// One window of <see cref="TimelineWindowDays"/> ending on the last day of each of the
    /// <see cref="TimelineMonths"/> months up to the one holding the last day, oldest first. The last day is
    /// <paramref name="to"/>, or yesterday when <paramref name="to"/> is not over: today still sells, and on the
    /// 1st that moves the last column to the month before.
    /// </summary>
    public static IReadOnlyList<AnalyticsTimelineWindow> TimelineWindows(DateOnly to, DateOnly today)
    {
        var last = to < today ? to : today.AddDays(-1);
        var lastMonth = new DateOnly(last.Year, last.Month, 1);

        return Enumerable.Range(0, TimelineMonths)
            .Select(i =>
            {
                var month = lastMonth.AddMonths(i - TimelineMonths + 1);
                var monthEnd = month.AddMonths(1).AddDays(-1);
                var end = monthEnd < last ? monthEnd : last;
                return new AnalyticsTimelineWindow(month, end.AddDays(1 - TimelineWindowDays), end, end < monthEnd);
            })
            .ToList();
    }

    /// <summary>Up to 31 days — day, up to six months — week, longer — month.</summary>
    public static AnalyticsStep DefaultStep(DateOnly from, DateOnly to)
    {
        if (to.DayNumber - from.DayNumber + 1 <= 31) return AnalyticsStep.Day;
        return to < from.AddMonths(6) ? AnalyticsStep.Week : AnalyticsStep.Month;
    }

    /// <summary>
    /// Sums amounts into <c>boundaries.Count + 1</c> age buckets: below the first boundary, between each
    /// neighbouring pair, and at or above the last. A boundary belongs to the bucket it opens.
    /// </summary>
    public static decimal[] SplitByAge(IEnumerable<(int AgeDays, decimal Amount)> amounts, IReadOnlyList<int> boundaries)
    {
        var buckets = new decimal[boundaries.Count + 1];
        foreach (var (age, amount) in amounts)
            buckets[boundaries.Count(b => b <= age)] += amount;
        return buckets;
    }

    /// <summary>
    /// Average per occurrence of each weekday, Monday first: the sum over all Mondays of the period divided
    /// by how many Mondays it has — a month can hold five Mondays and four Tuesdays. Null for a weekday the
    /// period does not contain. Values dated outside the period are ignored.
    /// </summary>
    public static decimal?[] WeekdayAverages(DateOnly from, DateOnly to, IEnumerable<(DateOnly Day, decimal Value)> values)
    {
        var sums = new decimal[7];
        var occurrences = new int[7];

        for (var day = from; day <= to; day = day.AddDays(1))
            occurrences[WeekdayIndex(day)]++;

        foreach (var (day, value) in values)
            if (day >= from && day <= to)
                sums[WeekdayIndex(day)] += value;

        return Enumerable.Range(0, 7)
            .Select(i => occurrences[i] == 0 ? (decimal?)null : sums[i] / occurrences[i])
            .ToArray();
    }

    /// <summary>
    /// A period whose end is closer to today than the maturity window still collects returns, so its return
    /// share reads low.
    /// </summary>
    public static bool IsReturnsImmature(DateOnly to, DateOnly today, int maturityDays) =>
        today.DayNumber - to.DayNumber < maturityDays;

    /// <summary>Weighted by money, not averaged over lines: a cheap item at −90% must not weigh as much as an expensive one.</summary>
    public static double? DiscountDepth(decimal priceSum, decimal oldPriceSum) =>
        oldPriceSum <= 0 ? null : 1 - (double)(priceSum / oldPriceSum);

    public static double? Ratio(decimal part, decimal whole) => whole == 0 ? null : (double)(part / whole);

    private static int WeekdayIndex(DateOnly day) => ((int)day.DayOfWeek + 6) % 7;

    private static int IntervalIndex(IReadOnlyList<AnalyticsInterval> intervals, DateOnly day)
    {
        int low = 0, high = intervals.Count - 1;
        while (low <= high)
        {
            var mid = (low + high) / 2;
            if (day < intervals[mid].Start) high = mid - 1;
            else if (day > intervals[mid].End) low = mid + 1;
            else return mid;
        }

        return -1;
    }

    private static DateOnly StartOf(DateOnly day, AnalyticsStep step) => step switch
    {
        AnalyticsStep.Day => day,
        AnalyticsStep.Week => day.AddDays(-WeekdayIndex(day)),
        _ => new DateOnly(day.Year, day.Month, 1),
    };
}

public sealed record AbcRankedItem<TKey>(TKey Id, decimal Value, double Share, double CumulativeShare, AbcClass Class);

/// <summary>Inclusive bounds, clamped to the period.</summary>
public sealed record AnalyticsInterval(DateOnly Start, DateOnly End, bool IsPartial);

/// <param name="Month">First day of the month the window stands for.</param>
/// <param name="From">Inclusive.</param>
/// <param name="To">Inclusive.</param>
/// <param name="IsPartial">The window ends before the month does.</param>
public sealed record AnalyticsTimelineWindow(DateOnly Month, DateOnly From, DateOnly To, bool IsPartial);
