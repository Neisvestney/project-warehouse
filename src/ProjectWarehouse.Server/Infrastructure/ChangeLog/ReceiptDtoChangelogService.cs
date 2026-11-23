using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Models.Receipts;

namespace ProjectWarehouse.Server.Infrastructure.ChangeLog;

public class ReceiptDtoChangelogService(IChangeLogService changeLogService)
    : TypedChangeLogService<ReceiptDto>(changeLogService, AppEntityType.Receipt);
