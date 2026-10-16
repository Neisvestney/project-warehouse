using ProjectWarehouse.Server.Domain;

namespace ProjectWarehouse.Server.Models.Integrations;

/// <summary>
/// When ApiKey is supplied the route id is ignored, so a key can be checked before the account exists.
/// </summary>
public class TestConnectionRequest
{
    public MarketplaceType? Type { get; init; }
    public string? ClientId { get; init; }
    public string? ApiKey { get; init; }
}

public class TestConnectionResponse
{
    public bool IsValid { get; init; }
    public string? Message { get; init; }
}
