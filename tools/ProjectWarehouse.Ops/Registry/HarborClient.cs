using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ProjectWarehouse.Ops.Configuration;

namespace ProjectWarehouse.Ops.Registry;

public sealed class HarborClient(RegistryConfig config, RegistryCredential? credential, HttpClient http)
    : IRegistryClient
{
    public async Task<IReadOnlyList<string>> ListTagsAsync(string image, CancellationToken cancellationToken)
    {
        var url = $"{config.Url.TrimEnd('/')}/api/v2.0/projects/{Uri.EscapeDataString(config.Project)}"
            + $"/repositories/{Uri.EscapeDataString(image)}/artifacts"
            + "?with_tag=true&page_size=100&sort=-push_time";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (credential is not null)
        {
            var basic = Convert.ToBase64String(
                Encoding.UTF8.GetBytes($"{credential.Username}:{credential.Secret}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", basic);
        }

        using var response = await http.SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return [];

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            throw new RegistryException(
                credential is null
                    ? $"{config.Host} rejected an anonymous request. Run `docker login {config.Host}`."
                    : $"{config.Host} rejected the credentials for '{credential.Username}'.");
        }

        if (!response.IsSuccessStatusCode)
            throw new RegistryException($"{config.Host} returned {(int)response.StatusCode} for {url}.");

        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        return ParseTags(payload);
    }

    private static List<string> ParseTags(string payload)
    {
        using var document = JsonDocument.Parse(payload);
        var tags = new List<string>();

        foreach (var artifact in document.RootElement.EnumerateArray())
        {
            if (!artifact.TryGetProperty("tags", out var element)
                || element.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var tag in element.EnumerateArray())
            {
                if (tag.TryGetProperty("name", out var name) && name.GetString() is { } value)
                    tags.Add(value);
            }
        }

        return tags;
    }
}
