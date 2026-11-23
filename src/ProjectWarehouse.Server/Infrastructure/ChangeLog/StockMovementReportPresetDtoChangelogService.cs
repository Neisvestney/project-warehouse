using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Models.Statistics;

namespace ProjectWarehouse.Server.Infrastructure.ChangeLog;

public class StockMovementReportPresetDtoChangelogService(IChangeLogService changeLogService)
    : TypedChangeLogService<StockMovementReportPresetDto>(changeLogService, AppEntityType.StockMovementReportPreset);
