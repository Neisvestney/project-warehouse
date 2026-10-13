namespace Neisvestney.Marketplaces.Wildberries;

/// <summary>API token from the seller portal. One token covers every host its categories allow.</summary>
public sealed record WildberriesCredentials(string ApiToken)
{
    // The compiler-generated ToString would print ApiToken into any log or exception message it reaches
    private bool PrintMembers(System.Text.StringBuilder builder) => false;
}

/// <summary>
/// Resolves the credentials of the current request. Called per request, so one <see cref="HttpClient"/>
/// can serve many seller accounts.
/// </summary>
public interface IWildberriesCredentialsProvider
{
    WildberriesCredentials GetCredentials();
}
