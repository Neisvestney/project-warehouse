using System.Diagnostics.Metrics;
using ProjectWarehouse.Server.Services;

namespace ProjectWarehouse.Server.Infrastructure.Observability;

/// <summary>
/// Instruments of the optimistic-concurrency loop around a stock group write. They answer how hard the
/// warehouse fights itself for a node without walking traces by hand: the counter is the alerting signal
/// (a write that gave up wrote nothing), the histogram shows how deep the contention normally goes.
/// <para>
/// Node and item stay out of the tags — their cardinality is the warehouse itself, and a span already
/// carries both under <c>inventory.group_write.*</c>.
/// </para>
/// </summary>
public static class InventoryMetrics
{
    public const string AttemptsName = "inventory.group_write.attempts";

    /// <summary>
    /// Bucket edges for <see cref="AttemptsName" />, registered as a view. Every attempt count the loop can
    /// produce needs its own bucket: the default boundaries start at 5 and collapse the whole range into one.
    /// </summary>
    public static readonly double[] AttemptBoundaries =
        [.. Enumerable.Range(1, InventoryService.GroupWriteRetryLimit + 1).Select(a => (double)a)];

    private static readonly KeyValuePair<string, object?> Committed = new("inventory.group_write.outcome", "commit");
    private static readonly KeyValuePair<string, object?> Exhausted = new("inventory.group_write.outcome", "exhausted");

    // named after the retry budget, not after contention: a conflict that the next attempt wins is invisible here
    private static readonly Counter<long> Exhaustions = AppTelemetry.Meter.CreateCounter<long>(
        "inventory.group_write.exhausted",
        unit: "{write}",
        description: "Stock group writes that exhausted the retry budget and wrote nothing.");

    private static readonly Histogram<int> Attempts = AppTelemetry.Meter.CreateHistogram<int>(
        AttemptsName,
        unit: "{attempt}",
        description: "Attempts one stock group write took before it committed or gave up.");

    public static void RecordCommit(int attempts) => Attempts.Record(attempts, Committed);

    public static void RecordExhausted(int attempts)
    {
        Attempts.Record(attempts, Exhausted);
        Exhaustions.Add(1, Exhausted);
    }
}
