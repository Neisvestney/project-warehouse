using Neisvestney.Marketplaces.Wildberries;
using ProjectWarehouse.Server.Integrations.Ozon;

namespace ProjectWarehouse.Server.Integrations.Wildberries;

/// <summary>Feeds <see cref="WildberriesAuthHandler"/> the account the provider put in scope.</summary>
public class WildberriesRequestCredentials(MarketplaceRequestContext context) : IWildberriesCredentialsProvider
{
    public WildberriesCredentials GetCredentials()
    {
        var credentials = context.Credentials
            ?? throw new InvalidOperationException(
                "No marketplace credentials in scope. Call IWildberriesClient inside MarketplaceRequestContext.Use(...).");

        return new WildberriesCredentials(credentials.ApiKey);
    }
}
