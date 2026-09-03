using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ProjectWarehouse.Ops.Configuration;

namespace ProjectWarehouse.Ops.Registry;

/// Plain Distribution API, for registries without Harbor's management endpoints. The 401 carries
/// a WWW-Authenticate challenge naming a token service; the token is fetched and the call retried.
public sealed class DockerV2Client(RegistryConfig config, RegistryCredential? credential, HttpClient http)
    : IRegistryClient
{
    public async Task<IReadOnlyList<string>> ListTagsAsync(string image, CancellationToken cancellationToken)
    {
        var repository = $"{config.Project}/{image}";
        var url = $"{config.Url.TrimEnd('/')}/v2/{repository}/tags/list?n=100";

        var response = await SendAsync(url, null, cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            var challenge = response.Headers.WwwAuthenticate.FirstOrDefault();
            response.Dispose();

            var token = await AcquireTokenAsync(challenge, repository, cancellationToken)
                ?? throw new RegistryException(
                    $"{config.Host} requires authentication and issued no token. Run `docker login {config.Host}`.");

            response = await SendAsync(url, token, cancellationToken);
        }

        using (response)
        {
            if (response.StatusCode == HttpStatusCode.NotFound)
                return [];

            if (!response.IsSuccessStatusCode)
                throw new RegistryException($"{config.Host} returned {(int)response.StatusCode} for {url}.");

            var payload = await response.Content.ReadAsStringAsync(cancellationToken);
            using var document = JsonDocument.Parse(payload);

            if (!document.RootElement.TryGetProperty("tags", out var tags)
                || tags.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            return tags.EnumerateArray()
                .Select(tag => tag.GetString())
                .Where(tag => tag is not null)
                .Select(tag => tag!)
                .ToList();
        }
    }

    private async Task<HttpResponseMessage> SendAsync(
        string url, string? bearer, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);

        if (bearer is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        else if (credential is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Basic(credential));

        return await http.SendAsync(request, cancellationToken);
    }

    private async Task<string?> AcquireTokenAsync(
        AuthenticationHeaderValue? challenge, string repository, CancellationToken cancellationToken)
    {
        if (challenge?.Parameter is null
            || !challenge.Scheme.Equals("Bearer", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var parameters = ParseChallenge(challenge.Parameter);
        if (!parameters.TryGetValue("realm", out var realm))
            return null;

        var query = new List<string>();
        if (parameters.TryGetValue("service", out var service))
            query.Add($"service={Uri.EscapeDataString(service)}");

        query.Add($"scope={Uri.EscapeDataString($"repository:{repository}:pull")}");

        using var request = new HttpRequestMessage(HttpMethod.Get, $"{realm}?{string.Join('&', query)}");
        if (credential is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Basic(credential));

        using var response = await http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            return null;

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));

        foreach (var name in new[] { "token", "access_token" })
        {
            if (document.RootElement.TryGetProperty(name, out var value) && value.GetString() is { } token)
                return token;
        }

        return null;
    }

    private static Dictionary<string, string> ParseChallenge(string parameter)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var part in parameter.Split(','))
        {
            var separator = part.IndexOf('=');
            if (separator <= 0)
                continue;

            values[part[..separator].Trim()] = part[(separator + 1)..].Trim().Trim('"');
        }

        return values;
    }

    private static string Basic(RegistryCredential credential) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes($"{credential.Username}:{credential.Secret}"));
}
