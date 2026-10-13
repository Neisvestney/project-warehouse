namespace Neisvestney.Marketplaces.Ozon;

/// <summary>Seller API credentials. <see cref="ClientId"/> is omitted from the request when empty.</summary>
public sealed record OzonCredentials(string? ClientId, string ApiKey)
{
    // The compiler-generated ToString would print ApiKey into any log or exception message it reaches
    private bool PrintMembers(System.Text.StringBuilder builder) => false;
}

/// <summary>
/// Resolves the credentials of the current request. Called per request, so one <see cref="HttpClient"/>
/// can serve many seller accounts.
/// </summary>
public interface IOzonCredentialsProvider
{
    OzonCredentials GetCredentials();
}
