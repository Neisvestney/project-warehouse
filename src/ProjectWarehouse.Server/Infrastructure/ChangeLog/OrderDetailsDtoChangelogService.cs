using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Models.Orders;

namespace ProjectWarehouse.Server.Infrastructure.ChangeLog;

public class OrderDetailsDtoChangelogService(IChangeLogService changeLogService)
    : TypedChangeLogService<OrderDetailsDto>(changeLogService, AppEntityType.Order);
