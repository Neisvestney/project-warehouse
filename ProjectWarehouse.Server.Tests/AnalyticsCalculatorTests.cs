using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Infrastructure;
using ProjectWarehouse.Server.Models.Analytics;

namespace ProjectWarehouse.Server.Tests;

public class AnalyticsCalculatorTests
{
    private static readonly Guid Id1 = new("00000000-0000-0000-0000-000000000001");
    private static readonly Guid Id2 = new("00000000-0000-0000-0000-000000000002");
    private static readonly Guid Id3 = new("00000000-0000-0000-0000-000000000003");
    private static readonly Guid Id4 = new("00000000-0000-0000-0000-000000000004");

    [Fact]
    public void Resolve_NullFieldsFallBackToDefaults()
    {
        var options = AnalyticsCalculator.Resolve(new AnalyticsSettings { AbcBoundaryA = 70m, XyzStep = AnalyticsXyzStep.Month });

        Assert.Equal(70m, options.AbcBoundaryA);
        Assert.Equal(AnalyticsCalculator.DefaultAbcBoundaryB, options.AbcBoundaryB);
        Assert.Equal(AnalyticsXyzStep.Month, options.XyzStep);
        Assert.Equal(AnalyticsCalculator.DefaultPayoutAgeBoundaries, options.PayoutAgeBoundaries);
    }

    [Fact]
    public void PreviousPeriod_HasSameLengthAndEndsTheDayBefore()
    {
        var (from, to) = AnalyticsCalculator.PreviousPeriod(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30));

        Assert.Equal(new DateOnly(2026, 8, 2), from);
        Assert.Equal(new DateOnly(2026, 8, 31), to);
    }

    [Fact]
    public void RankAbc_ClassesByPrecedingShare()
    {
        var ranked = AnalyticsCalculator.RankAbc(
            [(Id1, 50m), (Id2, 30m), (Id3, 15m), (Id4, 5m)], 80m, 95m);

        // Preceding shares: 0, 50, 80, 95
        Assert.Equal([AbcClass.A, AbcClass.A, AbcClass.B, AbcClass.C], ranked.Select(r => r.Class));
        Assert.Equal(1.0, ranked[^1].CumulativeShare, 10);
        Assert.Equal(0.3, ranked[1].Share, 10);
    }

    [Fact]
    public void RankAbc_FirstPositionIsAlwaysA()
    {
        var ranked = AnalyticsCalculator.RankAbc([(Id1, 99m), (Id2, 1m)], 80m, 95m);

        Assert.Equal(AbcClass.A, ranked[0].Class);
        Assert.Equal(AbcClass.C, ranked[1].Class);
    }

    [Fact]
    public void RankAbc_DropsZerosAndBreaksTiesById()
    {
        var ranked = AnalyticsCalculator.RankAbc([(Id3, 10m), (Id2, 0m), (Id1, 10m)], 80m, 95m);

        Assert.Equal([Id1, Id3], ranked.Select(r => r.Id));
    }

    [Fact]
    public void RankAbc_EmptyInput()
    {
        Assert.Empty(AnalyticsCalculator.RankAbc([], 80m, 95m));
    }

    [Fact]
    public void CoefficientOfVariation_UsesPopulationDeviation()
    {
        // mean 5, population σ 2
        var cv = AnalyticsCalculator.CoefficientOfVariation([2, 4, 4, 4, 5, 5, 7, 9]);

        Assert.NotNull(cv);
        Assert.Equal(0.4, cv.Value, 10);
    }

    [Fact]
    public void CoefficientOfVariation_NullForZeroMeanOrEmpty()
    {
        Assert.Null(AnalyticsCalculator.CoefficientOfVariation([0, 0, 0]));
        Assert.Null(AnalyticsCalculator.CoefficientOfVariation([]));
    }

    [Theory]
    [InlineData(0.10, XyzClass.X)]
    [InlineData(0.1001, XyzClass.Y)]
    [InlineData(0.25, XyzClass.Y)]
    [InlineData(0.26, XyzClass.Z)]
    public void ClassifyXyz_BoundariesAreInclusive(double cv, XyzClass expected)
    {
        Assert.Equal(expected, AnalyticsCalculator.ClassifyXyz(cv, 10m, 25m));
    }

    [Fact]
    public void ClassifyXyz_NullCv()
    {
        Assert.Null(AnalyticsCalculator.ClassifyXyz(null, 10m, 25m));
    }

    [Fact]
    public void SplitIntervals_WeeksStartOnMondayAndFlagPartialEdges()
    {
        // 2026-09-02 is a Wednesday, 2026-09-20 a Sunday
        var intervals = AnalyticsCalculator.SplitIntervals(
            new DateOnly(2026, 9, 2), new DateOnly(2026, 9, 20), AnalyticsStep.Week);

        Assert.Equal(
            [
                new AnalyticsInterval(new DateOnly(2026, 9, 2), new DateOnly(2026, 9, 6), true),
                new AnalyticsInterval(new DateOnly(2026, 9, 7), new DateOnly(2026, 9, 13), false),
                new AnalyticsInterval(new DateOnly(2026, 9, 14), new DateOnly(2026, 9, 20), false),
            ],
            intervals);
    }

    [Fact]
    public void SplitIntervals_Months()
    {
        var intervals = AnalyticsCalculator.SplitIntervals(
            new DateOnly(2026, 1, 1), new DateOnly(2026, 3, 15), AnalyticsStep.Month);

        Assert.Equal(3, intervals.Count);
        Assert.Equal(new DateOnly(2026, 2, 28), intervals[1].End);
        Assert.False(intervals[1].IsPartial);
        Assert.True(intervals[2].IsPartial);
        Assert.Equal(new DateOnly(2026, 3, 15), intervals[2].End);
    }

    [Fact]
    public void SplitIntervals_Days()
    {
        var intervals = AnalyticsCalculator.SplitIntervals(
            new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 3), AnalyticsStep.Day);

        Assert.Equal(3, intervals.Count);
        Assert.All(intervals, i => Assert.False(i.IsPartial));
    }

    [Fact]
    public void FullIntervals_DropPartialEdges()
    {
        var intervals = AnalyticsCalculator.FullIntervals(
            new DateOnly(2026, 9, 2), new DateOnly(2026, 9, 24), AnalyticsXyzStep.Week, new DateOnly(2026, 10, 1));

        Assert.Equal([new DateOnly(2026, 9, 7), new DateOnly(2026, 9, 14)], intervals.Select(i => i.Start));
    }

    [Fact]
    public void FullIntervals_DropWeeksNotOverYet()
    {
        // The week of Sep 14 ends on Sunday the 20th, so on that day it is still selling
        var intervals = AnalyticsCalculator.FullIntervals(
            new DateOnly(2026, 9, 2), new DateOnly(2026, 9, 24), AnalyticsXyzStep.Week, new DateOnly(2026, 9, 20));

        Assert.Equal([new DateOnly(2026, 9, 7)], intervals.Select(i => i.Start));
    }

    [Fact]
    public void IntervalsSince_StartsAtTheWeekOfTheFirstSale()
    {
        var intervals = AnalyticsCalculator.FullIntervals(
            new DateOnly(2026, 9, 7), new DateOnly(2026, 9, 27), AnalyticsXyzStep.Week, new DateOnly(2026, 10, 1));

        var since = AnalyticsCalculator.IntervalsSince(intervals, new DateOnly(2026, 9, 16));

        Assert.Equal([new DateOnly(2026, 9, 14), new DateOnly(2026, 9, 21)], since.Select(i => i.Start));
    }

    [Fact]
    public void TimelineWindows_EndAtMonthEndsAndStopBeforeToday()
    {
        var windows = AnalyticsCalculator.TimelineWindows(new DateOnly(2026, 9, 30), new DateOnly(2026, 9, 27));

        Assert.Equal(12, windows.Count);
        Assert.Equal(new DateOnly(2025, 10, 1), windows[0].Month);
        Assert.Equal(new DateOnly(2025, 10, 31), windows[0].To);
        Assert.Equal(new DateOnly(2025, 8, 2), windows[0].From);
        Assert.False(windows[0].IsPartial);
        Assert.Equal(new DateOnly(2026, 9, 1), windows[^1].Month);
        Assert.Equal(new DateOnly(2026, 9, 26), windows[^1].To);
        Assert.True(windows[^1].IsPartial);
        Assert.All(windows, w => Assert.Equal(AnalyticsCalculator.TimelineWindowDays, w.To.DayNumber - w.From.DayNumber + 1));
    }

    [Fact]
    public void TimelineWindows_CutTheLastAtThePeriodEnd()
    {
        var windows = AnalyticsCalculator.TimelineWindows(new DateOnly(2026, 6, 15), new DateOnly(2026, 9, 27));

        Assert.Equal(new DateOnly(2026, 6, 15), windows[^1].To);
        Assert.True(windows[^1].IsPartial);
        Assert.Equal(new DateOnly(2026, 5, 31), windows[^2].To);
    }

    [Fact]
    public void TimelineWindows_OnTheFirstEndWithThePreviousMonth()
    {
        var windows = AnalyticsCalculator.TimelineWindows(new DateOnly(2026, 10, 31), new DateOnly(2026, 10, 1));

        Assert.Equal(new DateOnly(2026, 9, 1), windows[^1].Month);
        Assert.Equal(new DateOnly(2026, 9, 30), windows[^1].To);
        Assert.False(windows[^1].IsPartial);
    }

    [Fact]
    public void TimelineWindows_EndOnTheLastDayOfFebruary()
    {
        var leap = AnalyticsCalculator.TimelineWindows(new DateOnly(2028, 2, 29), new DateOnly(2028, 6, 1));
        var common = AnalyticsCalculator.TimelineWindows(new DateOnly(2027, 2, 28), new DateOnly(2027, 6, 1));

        Assert.Equal(new DateOnly(2028, 2, 29), leap[^1].To);
        Assert.False(leap[^1].IsPartial);
        Assert.Equal(new DateOnly(2027, 2, 28), common[^1].To);
        Assert.False(common[^1].IsPartial);
        Assert.Equal(new DateOnly(2026, 11, 30), common[^1].From);
    }

    [Fact]
    public void SumByInterval_BucketsDaysAndNullsTheFuture()
    {
        var intervals = AnalyticsCalculator.SplitIntervals(
            new DateOnly(2026, 9, 1), new DateOnly(2026, 11, 30), AnalyticsStep.Month);

        var sums = AnalyticsCalculator.SumByInterval(intervals, new DateOnly(2026, 10, 5),
        [
            (new DateOnly(2026, 9, 1), 2),
            (new DateOnly(2026, 9, 30), 3),
            (new DateOnly(2026, 10, 4), 4),
            (new DateOnly(2026, 8, 31), 100),
        ]);

        Assert.Equal([5, 4, null], sums);
    }

    [Theory]
    [InlineData("2026-09-01", "2026-10-01", AnalyticsStep.Day)]
    [InlineData("2026-09-01", "2026-10-02", AnalyticsStep.Week)]
    [InlineData("2026-01-01", "2026-06-30", AnalyticsStep.Week)]
    [InlineData("2026-01-01", "2026-07-01", AnalyticsStep.Month)]
    public void DefaultStep_DependsOnPeriodLength(string from, string to, AnalyticsStep expected)
    {
        Assert.Equal(expected, AnalyticsCalculator.DefaultStep(DateOnly.Parse(from), DateOnly.Parse(to)));
    }

    [Fact]
    public void SplitByAge_BoundaryOpensItsBucket()
    {
        var buckets = AnalyticsCalculator.SplitByAge(
            [(0, 1m), (6, 2m), (7, 4m), (13, 8m), (14, 16m), (30, 32m), (400, 64m)], [7, 14, 30]);

        Assert.Equal([3m, 12m, 16m, 96m], buckets);
    }

    [Fact]
    public void WeekdayAverages_DivideByOccurrencesInPeriod()
    {
        // September 2026: Tuesday the 1st, so five Tuesdays and four Mondays
        var from = new DateOnly(2026, 9, 1);
        var to = new DateOnly(2026, 9, 30);
        var values = Enumerable.Range(0, 30).Select(i => (from.AddDays(i), 1m)).ToList();
        values.Add((new DateOnly(2026, 10, 5), 100m));

        var averages = AnalyticsCalculator.WeekdayAverages(from, to, values);

        Assert.All(averages, a => Assert.Equal(1m, a));
    }

    [Fact]
    public void WeekdayAverages_NullForMissingWeekday()
    {
        // Monday and Tuesday only
        var averages = AnalyticsCalculator.WeekdayAverages(
            new DateOnly(2026, 9, 7), new DateOnly(2026, 9, 8), [(new DateOnly(2026, 9, 7), 6m)]);

        Assert.Equal(6m, averages[0]);
        Assert.Equal(0m, averages[1]);
        Assert.All(averages.Skip(2), a => Assert.Null(a));
    }

    [Theory]
    [InlineData("2026-09-10", "2026-09-30", 21, true)]
    [InlineData("2026-09-09", "2026-09-30", 21, false)]
    public void IsReturnsImmature_ComparesEndToToday(string to, string today, int days, bool expected)
    {
        Assert.Equal(expected, AnalyticsCalculator.IsReturnsImmature(DateOnly.Parse(to), DateOnly.Parse(today), days));
    }

    [Fact]
    public void DiscountDepth_IsWeightedByMoney()
    {
        // 100 → 10 on a cheap item and 1000 → 900 on an expensive one: 1 − 910 / 1100
        Assert.Equal(1 - 910.0 / 1100.0, AnalyticsCalculator.DiscountDepth(910m, 1100m)!.Value, 10);
        Assert.Null(AnalyticsCalculator.DiscountDepth(0m, 0m));
    }
}
