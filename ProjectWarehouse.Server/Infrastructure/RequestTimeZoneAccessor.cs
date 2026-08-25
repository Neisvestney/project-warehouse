using Microsoft.AspNetCore.Http;

namespace ProjectWarehouse.Server.Infrastructure;

/// <summary>
/// The caller's own time zone, taken from the <c>X-Time-Zone</c> header. Null when there is no request
/// at all — a background job resolves to nothing here and the chain falls through to the server zone
/// without a single "are we in a job" check.
/// </summary>
public interface IRequestTimeZoneAccessor
{
    TimeZoneInfo? TimeZone { get; }
}

public class RequestTimeZoneAccessor(IHttpContextAccessor httpContextAccessor, ILogger<RequestTimeZoneAccessor> logger)
    : IRequestTimeZoneAccessor
{
    public const string HeaderName = "X-Time-Zone";

    private bool _resolved;
    private TimeZoneInfo? _timeZone;

    public TimeZoneInfo? TimeZone
    {
        get
        {
            if (_resolved) return _timeZone;
            _resolved = true;

            var raw = httpContextAccessor.HttpContext?.Request.Headers[HeaderName].ToString();
            if (string.IsNullOrWhiteSpace(raw)) return _timeZone = null;

            // Garbage in the header is not a 400: it is set by client code, and letting it fail the
            // request hands an outsider the ability to break every endpoint.
            if (!TimeZoneInfo.TryFindSystemTimeZoneById(raw, out var zone))
            {
                logger.LogDebug("Unknown time zone {TimeZoneId} in {Header}, falling back", raw, HeaderName);
                return _timeZone = null;
            }

            return _timeZone = zone;
        }
    }
}
