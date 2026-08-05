namespace ProjectWarehouse.Server.Integrations.Ozon;

/// <summary>
/// Supplies Client-Id / Api-Key. The generated client cannot: both headers are stripped from the spec
/// during generation so they never leak into every method signature.
/// </summary>
public class OzonAuthHandler(MarketplaceRequestContext context) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var credentials = context.Credentials
            ?? throw new InvalidOperationException(
                "No marketplace credentials in scope. Call IOzonClient inside MarketplaceRequestContext.Use(...).");

        if (!string.IsNullOrEmpty(credentials.ClientId))
            request.Headers.TryAddWithoutValidation("Client-Id", credentials.ClientId);
        request.Headers.TryAddWithoutValidation("Api-Key", credentials.ApiKey);

        return base.SendAsync(request, cancellationToken);
    }
}
