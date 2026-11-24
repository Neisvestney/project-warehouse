using System.ComponentModel.DataAnnotations;
using ProjectWarehouse.Server.Domain;

namespace ProjectWarehouse.Server.Models.Integrations;

public record YandexMarketCampaignDto(long CampaignId, long BusinessId, string Name, string? BusinessName,
    YandexMarketModel Model);

public class DiscoverYandexMarketCampaignsRequest
{
    [Required, MinLength(1)]
    public string ApiKey { get; init; } = null!;
}
