using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Models.Integrations;

namespace ProjectWarehouse.Server.Infrastructure.ChangeLog;

public class MarketplaceAccountDtoChangelogService(IChangeLogService changeLogService)
    : TypedChangeLogService<MarketplaceAccountDto>(changeLogService, AppEntityType.MarketplaceAccount);
