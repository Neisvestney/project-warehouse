using System.ComponentModel.DataAnnotations;
using ProjectWarehouse.Server.Domain;

namespace ProjectWarehouse.Server.Models.Integrations;

public class CreateMarketplaceAccountRequest
{
    public MarketplaceType Type { get; init; }

    /// <summary>Required when the provider declares RequiresClientId.</summary>
    public string? ClientId { get; init; }

    [Required, MinLength(1)]
    public string ApiKey { get; init; } = null!;

    [Range(1, 10080)]
    public int? SyncIntervalMinutes { get; init; }

    public bool IsActive { get; init; } = true;
}
