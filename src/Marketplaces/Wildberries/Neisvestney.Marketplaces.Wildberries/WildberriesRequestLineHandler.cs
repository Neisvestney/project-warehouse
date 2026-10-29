namespace Neisvestney.Marketplaces.Wildberries;

/// <summary>
/// Stamps the method and path of the request onto its response. The generated client copies response headers into
/// <see cref="WildberriesApiException.Headers"/> and nothing of the request, so this is how a failure names its call.
/// </summary>
public class WildberriesRequestLineHandler : DelegatingHandler
{
    public const string HeaderName = "X-Wildberries-Request";

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken);
        // path only: the query is not needed to tell the calls apart
        response.Headers.TryAddWithoutValidation(HeaderName, $"{request.Method} {request.RequestUri?.AbsolutePath}");
        return response;
    }

    public static string? Read(WildberriesApiException ex) =>
        ex.Headers.TryGetValue(HeaderName, out var values) ? values.FirstOrDefault() : null;
}
