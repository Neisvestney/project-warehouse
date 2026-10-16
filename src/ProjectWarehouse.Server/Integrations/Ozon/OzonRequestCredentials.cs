using Neisvestney.Marketplaces.Ozon;

namespace ProjectWarehouse.Server.Integrations.Ozon;

/// <summary>Feeds <see cref="OzonAuthHandler"/> the account the provider put in scope.</summary>
public class OzonRequestCredentials(MarketplaceRequestContext context) : IOzonCredentialsProvider
{
    public OzonCredentials GetCredentials()
    {
        var credentials = context.Credentials
            ?? throw new InvalidOperationException(
                "No marketplace credentials in scope. Call IOzonClient inside MarketplaceRequestContext.Use(...).");

        return new OzonCredentials(credentials.ClientId, credentials.ApiKey);
    }
}
