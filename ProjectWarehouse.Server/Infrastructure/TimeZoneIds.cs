namespace ProjectWarehouse.Server.Infrastructure;

/// <summary>
/// Shared handling of a stored IANA identifier. Both write paths of <c>Warehouse.TimeZoneId</c> — the
/// warehouse card and the forecast settings — go through here so a value typed in one form cannot be
/// stored differently from the same value typed in the other.
/// </summary>
public static class TimeZoneIds
{
    /// <summary>Blank means "not set", never an empty string.</summary>
    public static string? Normalize(string? raw) =>
        string.IsNullOrWhiteSpace(raw) ? null : raw.Trim();

    public static bool IsKnown(string id) => TimeZoneInfo.TryFindSystemTimeZoneById(id, out _);
}
