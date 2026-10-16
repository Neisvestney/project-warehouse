using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Serilog.Context;
using Serilog.Core;
using Serilog.Core.Enrichers;

namespace ProjectWarehouse.Server.Infrastructure.Observability;

/// <summary>
/// Request attributes the standard instrumentation does not know about. They go into the span, so traces
/// can be filtered by user, and into <see cref="LogContext" />, for lines logged outside the HTTP span.
/// </summary>
public class TelemetryEnrichmentMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IRequestTimeZoneAccessor timeZoneAccessor)
    {
        var values = new (string Name, string? Value)[]
        {
            ("user.id", context.User.FindFirstValue(JwtRegisteredClaimNames.Sub)),
            ("user.name", context.User.FindFirstValue("name")),
            ("app.time_zone", timeZoneAccessor.TimeZone?.Id),
        };

        var activity = Activity.Current;
        var enrichers = new List<ILogEventEnricher>(values.Length + 1);

        foreach (var (name, value) in values)
        {
            if (string.IsNullOrEmpty(value)) continue;
            activity?.SetTag(name, value);
            enrichers.Add(new PropertyEnricher(name, value));
        }

        // the span gets url.path from the AspNetCore instrumentation; a log record has no path of its
        // own, and the collector's filter/noise for logs has nothing else to match on
        if (context.Request.Path.HasValue)
            enrichers.Add(new PropertyEnricher("url.path", context.Request.Path.Value));

        using (LogContext.Push([.. enrichers]))
            await next(context);
    }
}
