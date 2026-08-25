namespace ProjectWarehouse.Server.Infrastructure;

public static class TimeZoneExtensions
{
    /// <summary>
    /// Collapses the zone to a fixed offset for the whole calculation. A DST transition inside the range
    /// gives its day 23 or 25 hours and moves a handful of movements into the neighbouring day; the price
    /// of keeping the grouping a plain <c>AddMinutes</c> that Npgsql translates.
    /// </summary>
    public static int CurrentOffsetMinutes(this TimeZoneInfo zone) =>
        (int)zone.GetUtcOffset(DateTime.UtcNow).TotalMinutes;

    /// <summary>
    /// IANA identifier of the zone. Windows resolves an IANA id into a Windows one, so <c>Id</c> alone
    /// would hand the client <c>Russian Standard Time</c> on a dev box and <c>Europe/Moscow</c> in the container.
    /// </summary>
    public static string IanaId(this TimeZoneInfo zone) =>
        zone.HasIanaId ? zone.Id
        : TimeZoneInfo.TryConvertWindowsIdToIanaId(zone.Id, out var iana) ? iana
        : zone.Id;
}
