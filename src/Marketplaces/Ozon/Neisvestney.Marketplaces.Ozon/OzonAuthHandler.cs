namespace Neisvestney.Marketplaces.Ozon;

/// <summary>
/// Supplies Client-Id / Api-Key. The generated client cannot: both headers are stripped from the spec
/// during generation so they never leak into every method signature.
/// </summary>
public class OzonAuthHandler(IOzonCredentialsProvider credentialsProvider) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var credentials = credentialsProvider.GetCredentials();

        // Remove first: a request re-sent through this handler would otherwise carry the value twice
        request.Headers.Remove("Client-Id");
        request.Headers.Remove("Api-Key");
        if (!string.IsNullOrEmpty(credentials.ClientId))
            request.Headers.TryAddWithoutValidation("Client-Id", credentials.ClientId);
        request.Headers.TryAddWithoutValidation("Api-Key", credentials.ApiKey);

        return base.SendAsync(request, cancellationToken);
    }
}
