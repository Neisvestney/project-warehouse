namespace Neisvestney.Marketplaces.YandexMarket;

/// <summary>
/// Supplies the <c>Api-Key</c> header. The spec declares it as a security scheme rather than a parameter,
/// so the generated client never sets it.
/// </summary>
public class YandexMarketAuthHandler(IYandexMarketCredentialsProvider credentialsProvider) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var apiKey = credentialsProvider.GetCredentials()?.ApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("The credentials provider returned no Yandex Market API key.");

        // Remove first: a request re-sent through this handler would otherwise carry the key twice
        request.Headers.Remove("Api-Key");
        request.Headers.TryAddWithoutValidation("Api-Key", apiKey);
        return base.SendAsync(request, cancellationToken);
    }
}
