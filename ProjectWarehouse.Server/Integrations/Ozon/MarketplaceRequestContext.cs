using ProjectWarehouse.Server.Integrations.Abstractions;

namespace ProjectWarehouse.Server.Integrations.Ozon;

/// <summary>
/// Carries the credentials of the account being served from the provider down to the auth handler.
/// <para>
/// AsyncLocal, not a scoped service: IHttpClientFactory builds and caches message handlers in its own
/// DI scope, so a scoped context injected into <see cref="OzonAuthHandler"/> is a different instance
/// from the one the provider wrote to. The ambient value flows through the handler pipeline regardless.
/// One HttpClient serves many accounts, so the headers cannot live on DefaultRequestHeaders either.
/// </para>
/// </summary>
public class MarketplaceRequestContext
{
    private static readonly AsyncLocal<MarketplaceCredentials?> Current = new();

    public MarketplaceCredentials? Credentials => Current.Value;

    public IDisposable Use(MarketplaceCredentials credentials)
    {
        var previous = Current.Value;
        Current.Value = credentials;
        return new Scope(previous);
    }

    private sealed class Scope(MarketplaceCredentials? previous) : IDisposable
    {
        public void Dispose() => Current.Value = previous;
    }
}
