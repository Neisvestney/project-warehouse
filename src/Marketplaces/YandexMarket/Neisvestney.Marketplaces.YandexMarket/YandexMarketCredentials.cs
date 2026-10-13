namespace Neisvestney.Marketplaces.YandexMarket;

/// <summary>Api-Key token issued in the seller cabinet. One token covers every campaign of its business.</summary>
public sealed record YandexMarketCredentials(string ApiKey)
{
    // The compiler-generated ToString would print ApiKey into any log or exception message it reaches
    private bool PrintMembers(System.Text.StringBuilder builder) => false;
}

/// <summary>
/// Resolves the credentials of the current request. Called per request, so one <see cref="HttpClient"/>
/// can serve many seller accounts.
/// </summary>
public interface IYandexMarketCredentialsProvider
{
    YandexMarketCredentials GetCredentials();
}
