using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Neisvestney.Marketplaces.Avito;

/// <summary>
/// Exchanges the account's client credentials for an OAuth2 access token (cached in <see cref="AvitoTokenCache"/>)
/// and sends it as <c>Authorization: Bearer</c>. The header is stripped from the spec during generation so it
/// never leaks into every method signature. A 401 drops the cached token and retries once with a fresh one.
/// </summary>
public class AvitoAuthHandler(IAvitoCredentialsProvider credentialsProvider, AvitoTokenCache tokenCache) : DelegatingHandler
{
    public static readonly Uri TokenEndpoint = new(AvitoHosts.Api, "token");

    // Avito issues day-long tokens; the cap keeps an absurd expires_in from overflowing TimeSpan
    private static readonly TimeSpan MaxTokenLifetime = TimeSpan.FromDays(1);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var credentials = credentialsProvider.GetCredentials()
            ?? throw new InvalidOperationException("The credentials provider returned no Avito credentials.");
        if (string.IsNullOrWhiteSpace(credentials.ClientId) || string.IsNullOrWhiteSpace(credentials.ClientSecret))
            throw new InvalidOperationException("Avito credentials need both a client id and a client secret.");

        // only buffered content can go out twice; a stream would be re-sent empty
        var canRetry = request.Content is null or ByteArrayContent;

        for (var attempt = 0; ; attempt++)
        {
            var token = await tokenCache.GetAsync(credentials.ClientId, ct => RequestTokenAsync(credentials, ct), cancellationToken);

            // Remove first: a request re-sent through this handler would otherwise carry the token twice
            request.Headers.Remove("Authorization");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await base.SendAsync(request, cancellationToken);
            if (response.StatusCode != HttpStatusCode.Unauthorized)
                return response;

            // dropped even when not retrying, so the next request fetches a fresh token instead of reusing a revoked one
            tokenCache.Invalidate(credentials.ClientId, token);
            // one retry: a revoked account must not loop through token requests
            if (attempt > 0 || !canRetry)
                return response;
            response.Dispose();
        }
    }

    // Sent down the same pipeline: it shares the handlers below this one but never passes through this one again
    private async Task<(string Token, TimeSpan Lifetime)> RequestTokenAsync(AvitoCredentials credentials, CancellationToken cancellationToken)
    {
        using var tokenRequest = new HttpRequestMessage(HttpMethod.Post, TokenEndpoint)
        {
            Content = new FormUrlEncodedContent(
            [
                new("grant_type", "client_credentials"),
                new("client_id", credentials.ClientId),
                new("client_secret", credentials.ClientSecret),
            ]),
        };
        using var response = await base.SendAsync(tokenRequest, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        // a rejected client may come back as a JSON `error` object, so a success status alone proves nothing
        if (response.IsSuccessStatusCode && TryParseToken(body) is { } parsed)
            return parsed;

        var headers = response.Headers.Concat(response.Content.Headers)
            .ToDictionary(h => h.Key, h => h.Value, StringComparer.OrdinalIgnoreCase);
        throw new AvitoApiException("Avito token request failed.", (int)response.StatusCode, body, headers, null);
    }

    private static (string Token, TimeSpan Lifetime)? TryParseToken(string body)
    {
        try
        {
            using var json = JsonDocument.Parse(body);
            var root = json.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("access_token", out var accessToken)
                || accessToken.ValueKind != JsonValueKind.String
                || accessToken.GetString() is not { Length: > 0 } token)
                return null;

            var seconds = root.TryGetProperty("expires_in", out var expiresIn)
                && expiresIn.ValueKind == JsonValueKind.Number && expiresIn.TryGetInt64(out var value) && value > 0
                    ? value
                    : 3600;
            return (token, TimeSpan.FromSeconds(Math.Min(seconds, (long)MaxTokenLifetime.TotalSeconds)));
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
