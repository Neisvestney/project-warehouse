using System.ComponentModel.DataAnnotations;

namespace ProjectWarehouse.Server.Models.Integrations;

public record YandexMarketCampaignDto(long CampaignId, long BusinessId, string Name, string? BusinessName);

public class DiscoverYandexMarketCampaignsRequest
{
    [Required, MinLength(1)]
    public string ApiKey { get; init; } = null!;
}
