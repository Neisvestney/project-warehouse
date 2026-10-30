using ProjectWarehouse.Server.Integrations.Wildberries;

namespace ProjectWarehouse.Server.Tests;

public class WildberriesReturnLinkerTests
{
    private static readonly DateOnly Requested = new(2026, 9, 25);

    [Fact]
    public void Resolve_SelectsLatestPrecedingCustomerMovement()
    {
        Assert.Equal("second", WildberriesReturnLinker.Resolve(Requested,
        [
            new("first", new(2026, 7, 1), null),
            new("second", new(2026, 9, 17), null),
            new("future", new(2026, 10, 1), null),
        ]));
    }

    [Fact]
    public void Resolve_UsesEventTimeInsteadOfLaterAccountingDay()
    {
        Assert.Equal("second", WildberriesReturnLinker.Resolve(Requested,
        [
            new("first", new(2026, 10, 1), Utc(17, 8)),
            new("second", new(2026, 9, 18), Utc(18, 8)),
        ]));
    }

    [Fact]
    public void Resolve_OrdersKnownTimesWithinSameMoscowDay()
    {
        Assert.Equal("second", WildberriesReturnLinker.Resolve(Requested,
        [
            new("first", new(2026, 9, 17), Utc(17, 8)),
            new("second", new(2026, 9, 17), Utc(17, 9)),
        ]));
    }

    [Fact]
    public void Resolve_RefusesTiedTimesAcrossPostings()
    {
        Assert.Null(WildberriesReturnLinker.Resolve(Requested,
        [
            new("first", new(2026, 9, 17), Utc(17, 8)),
            new("second", new(2026, 9, 17), Utc(17, 8)),
        ]));
    }

    [Fact]
    public void Resolve_RefusesUnknownTimeAcrossPostingsOnLatestDay()
    {
        Assert.Null(WildberriesReturnLinker.Resolve(Requested,
        [
            new("first", new(2026, 9, 17), Utc(17, 8)),
            new("second", new(2026, 9, 17), null),
        ]));
    }

    [Fact]
    public void Resolve_AllowsRepeatedEventsOfSamePostingWithoutTimes()
    {
        Assert.Equal("first", WildberriesReturnLinker.Resolve(Requested,
        [
            new("first", new(2026, 9, 17), Utc(17, 8)),
            new("first", new(2026, 9, 17), null),
        ]));
    }

    [Fact]
    public void Resolve_RefusesRequestDayBecauseRequestTimeIsUnknown()
    {
        Assert.Null(WildberriesReturnLinker.Resolve(Requested,
        [
            new("first", new(2026, 9, 17), null),
            new("second", new(2026, 9, 24), Utc(24, 22)),
        ]));
    }

    [Fact]
    public void Resolve_RefusesMissingRequestDateAndEmptyOrFutureHistory()
    {
        Assert.Null(WildberriesReturnLinker.Resolve(null, [new("first", Requested.AddDays(-1), null)]));
        Assert.Null(WildberriesReturnLinker.Resolve(Requested, []));
        Assert.Null(WildberriesReturnLinker.Resolve(Requested, [new("future", Requested.AddDays(1), null)]));
    }

    private static DateTime Utc(int day, int hour) => new(2026, 9, day, hour, 0, 0, DateTimeKind.Utc);
}
