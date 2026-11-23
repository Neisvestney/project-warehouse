using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Models.Integrations;

namespace ProjectWarehouse.Server.Infrastructure.ChangeLog;

public class MarketplaceCardDtoChangelogService(IChangeLogService changeLogService)
    : TypedChangeLogService<MarketplaceCardDto>(changeLogService, AppEntityType.MarketplaceCard);
