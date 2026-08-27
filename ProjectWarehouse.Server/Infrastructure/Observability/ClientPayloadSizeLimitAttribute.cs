using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;

namespace ProjectWarehouse.Server.Infrastructure.Observability;

/// <summary>
/// Caps the request body at <c>Observability:MaxClientPayloadBytes</c>. Exists because
/// <c>[RequestSizeLimit]</c> takes a compile-time constant and cannot read configuration.
/// </summary>
public sealed class ClientPayloadSizeLimitAttribute : Attribute, IFilterFactory
{
    public bool IsReusable => true;

    public IFilterMetadata CreateInstance(IServiceProvider serviceProvider) =>
        new ClientPayloadSizeLimitFilter(
            serviceProvider.GetRequiredService<IOptions<ObservabilityOptions>>());
}

internal sealed class ClientPayloadSizeLimitFilter(IOptions<ObservabilityOptions> options)
    : IResourceFilter
{
    public void OnResourceExecuting(ResourceExecutingContext context)
    {
        // read-only once the body has been read; the filter runs before model binding, so it is not
        var feature = context.HttpContext.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (feature is { IsReadOnly: false })
            feature.MaxRequestBodySize = options.Value.MaxClientPayloadBytes;
    }

    public void OnResourceExecuted(ResourceExecutedContext context)
    {
    }
}
