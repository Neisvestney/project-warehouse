using ProjectWarehouse.Ops.Configuration;
using Spectre.Console;

namespace ProjectWarehouse.Ops.Registry;

public sealed class RegistryClientFactory : IDisposable
{
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(30) };
    private readonly Dictionary<string, RegistryCredential?> _credentials = new(StringComparer.Ordinal);

    public IRegistryClient Create(RegistryConfig config)
    {
        var credential = ResolveCredential(config);

        return config.Api switch
        {
            RegistryApi.Harbor => new HarborClient(config, credential, _http),
            RegistryApi.DockerV2 => new DockerV2Client(config, credential, _http),
            _ => throw new RegistryException($"Unsupported registry api '{config.Api}'."),
        };
    }

    /// Resolves and caches credentials up front. `prompt` reads the terminal, and a prompt raised
    /// from inside a live display fights it for the screen.
    public void Prepare(RegistryConfig config) => ResolveCredential(config);

    public void Dispose() => _http.Dispose();

    private RegistryCredential? ResolveCredential(RegistryConfig config)
    {
        if (_credentials.TryGetValue(config.Host, out var cached))
            return cached;

        var credential = config.Credentials switch
        {
            RegistryCredentials.Inline => new RegistryCredential(config.Username!, config.Password!),
            RegistryCredentials.Prompt => Prompt(config.Host),
            _ => DockerCredentialStore.TryRead(config.Host),
        };

        _credentials[config.Host] = credential;
        return credential;
    }

    private static RegistryCredential Prompt(string host)
    {
        AnsiConsole.MarkupLineInterpolated($"[grey]Credentials for {host}[/]");
        var user = AnsiConsole.Ask<string>("  username:");
        var secret = AnsiConsole.Prompt(new TextPrompt<string>("  password:").Secret());
        return new RegistryCredential(user, secret);
    }
}
