namespace Neisvestney.Marketplaces.Wildberries;

/// <summary>
/// Supplies the token as a bare <c>Authorization</c> value — WB takes no "Bearer" scheme. The header is
/// stripped from the spec during generation so it never leaks into every method signature.
/// </summary>
public class WildberriesAuthHandler(IWildberriesCredentialsProvider credentialsProvider) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var apiToken = credentialsProvider.GetCredentials()?.ApiToken;
        if (string.IsNullOrWhiteSpace(apiToken))
            throw new InvalidOperationException("The credentials provider returned no Wildberries API token.");

        // Remove first: a request re-sent through this handler would otherwise carry the token twice
        request.Headers.Remove("Authorization");
        request.Headers.TryAddWithoutValidation("Authorization", apiToken);
        return base.SendAsync(request, cancellationToken);
    }
}
