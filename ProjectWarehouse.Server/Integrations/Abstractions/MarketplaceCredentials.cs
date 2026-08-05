namespace ProjectWarehouse.Server.Integrations.Abstractions;

// ClientId is null for providers that authenticate with a token alone (Wildberries).
public record MarketplaceCredentials(string? ClientId, string ApiKey);
