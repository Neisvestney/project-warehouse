using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace ProjectWarehouse.Ops.Registry;

/// Reads what `docker login` already stored. On Windows that is almost never the config file
/// itself — it holds `credsStore: wincred` and the secret lives in Credential Manager, reachable
/// only through the helper executable.
public static class DockerCredentialStore
{
    public static RegistryCredential? TryRead(string registryHost)
    {
        var configPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".docker", "config.json");

        if (!File.Exists(configPath))
            return null;

        using var document = JsonDocument.Parse(File.ReadAllText(configPath));
        var root = document.RootElement;

        var key = MatchKey(root, registryHost) ?? registryHost;

        if (root.TryGetProperty("credHelpers", out var helpers)
            && helpers.TryGetProperty(key, out var helper)
            && helper.GetString() is { } helperName)
        {
            return FromHelper(helperName, key);
        }

        if (root.TryGetProperty("auths", out var auths)
            && auths.TryGetProperty(key, out var entry)
            && entry.TryGetProperty("auth", out var auth)
            && auth.GetString() is { Length: > 0 } encoded)
        {
            return FromBase64(encoded);
        }

        if (root.TryGetProperty("credsStore", out var store) && store.GetString() is { } storeName)
            return FromHelper(storeName, key);

        return null;
    }

    /// config.json keys appear both bare and scheme-prefixed depending on how the login was done.
    private static string? MatchKey(JsonElement root, string registryHost)
    {
        foreach (var section in new[] { "auths", "credHelpers" })
        {
            if (!root.TryGetProperty(section, out var element)
                || element.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            foreach (var property in element.EnumerateObject())
            {
                var name = property.Name;
                var bare = name
                    .Replace("https://", string.Empty)
                    .Replace("http://", string.Empty)
                    .TrimEnd('/');

                if (string.Equals(bare, registryHost, StringComparison.OrdinalIgnoreCase))
                    return name;
            }
        }

        return null;
    }

    private static RegistryCredential? FromBase64(string encoded)
    {
        string decoded;
        try
        {
            decoded = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
        }
        catch (FormatException)
        {
            return null;
        }

        var separator = decoded.IndexOf(':');
        return separator <= 0
            ? null
            : new RegistryCredential(decoded[..separator], decoded[(separator + 1)..]);
    }

    private static RegistryCredential? FromHelper(string helperName, string serverUrl)
    {
        var info = new ProcessStartInfo
        {
            FileName = $"docker-credential-{helperName}",
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        info.ArgumentList.Add("get");

        try
        {
            using var process = Process.Start(info);
            if (process is null)
                return null;

            process.StandardInput.Write(serverUrl);
            process.StandardInput.Close();

            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0 || output.TrimStart().StartsWith("credentials not found", StringComparison.OrdinalIgnoreCase))
                return null;

            using var document = JsonDocument.Parse(output);
            var username = document.RootElement.GetProperty("Username").GetString();
            var secret = document.RootElement.GetProperty("Secret").GetString();

            return username is null || secret is null ? null : new RegistryCredential(username, secret);
        }
        catch (Exception ex) when (ex is not JsonException)
        {
            return null;
        }
    }
}
