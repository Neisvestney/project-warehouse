namespace Neisvestney.Marketplaces.Avito;

/// <summary>OAuth2 client credentials of a seller account, from the Avito developer portal.</summary>
public sealed record AvitoCredentials(string ClientId, string ClientSecret)
{
    // The compiler-generated ToString would print ClientSecret into any log or exception message it reaches
    private bool PrintMembers(System.Text.StringBuilder builder) => false;
}

/// <summary>
/// Resolves the credentials of the current request. Called per request, so one <see cref="HttpClient"/>
/// can serve many seller accounts.
/// </summary>
public interface IAvitoCredentialsProvider
{
    AvitoCredentials GetCredentials();
}
