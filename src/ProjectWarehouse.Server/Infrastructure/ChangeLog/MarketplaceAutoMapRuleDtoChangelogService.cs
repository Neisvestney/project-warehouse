using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Models.Integrations;

namespace ProjectWarehouse.Server.Infrastructure.ChangeLog;

public class MarketplaceAutoMapRuleDtoChangelogService(IChangeLogService changeLogService)
    : TypedChangeLogService<MarketplaceAutoMapRuleDto>(changeLogService, AppEntityType.MarketplaceAutoMapRule);
