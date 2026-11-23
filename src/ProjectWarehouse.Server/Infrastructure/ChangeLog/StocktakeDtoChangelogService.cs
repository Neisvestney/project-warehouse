using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Models.Stocktakes;

namespace ProjectWarehouse.Server.Infrastructure.ChangeLog;

public class StocktakeDtoChangelogService(IChangeLogService changeLogService)
    : TypedChangeLogService<StocktakeDto>(changeLogService, AppEntityType.Stocktake);
