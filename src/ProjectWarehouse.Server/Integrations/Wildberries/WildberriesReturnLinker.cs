namespace ProjectWarehouse.Server.Integrations.Wildberries;

public static class WildberriesReturnLinker
{
    public const string SellerAttemptPrefix = "wb-seller-return:";
    private static readonly TimeZoneInfo Moscow = TimeZoneInfo.FindSystemTimeZoneById("Europe/Moscow");

    public sealed record Movement(string PostingNumber, DateOnly Day, DateTime? OccurredAt);

    public static string? Resolve(DateOnly? requestedOn, IEnumerable<Movement> movements)
    {
        if (requestedOn is not { } cutoff)
            return null;

        var candidates = movements
            .Select(m => m with
            {
                Day = m.OccurredAt is { } at
                    ? DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(at, Moscow))
                    : m.Day,
            })
            .Where(m => m.Day <= cutoff)
            .ToList();
        if (candidates.Count == 0 || candidates.Any(m => m.Day == cutoff))
            return null;

        var latestDay = candidates.Max(m => m.Day);
        var latest = candidates.Where(m => m.Day == latestDay).ToList();
        if (latest.Select(m => m.PostingNumber).Distinct().ToList() is [var only])
            return only;
        if (latest.Any(m => m.OccurredAt is null))
            return null;

        var lastAt = latest.Max(m => m.OccurredAt);
        return latest.Where(m => m.OccurredAt == lastAt)
            .Select(m => m.PostingNumber).Distinct().ToList() is [var posting] ? posting : null;
    }
}
