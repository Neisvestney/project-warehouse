using Microsoft.EntityFrameworkCore;
using ProjectWarehouse.Server.Data;
using ProjectWarehouse.Server.Infrastructure;

namespace ProjectWarehouse.Server.Services;

public class WarehouseTimeZoneResolver(
    ApplicationDbContext db,
    IRequestTimeZoneAccessor requestTimeZone,
    ILogger<WarehouseTimeZoneResolver> logger) : IWarehouseTimeZoneResolver
{
    public async Task<TimeZoneInfo> ResolveAsync(Guid? warehouseId, CancellationToken ct = default)
    {
        if (warehouseId is { } id)
        {
            var storedId = await db.Warehouses
                .Where(w => w.Id == id)
                .Select(w => w.TimeZoneId)
                .FirstOrDefaultAsync(ct);

            if (!string.IsNullOrWhiteSpace(storedId))
            {
                if (TimeZoneInfo.TryFindSystemTimeZoneById(storedId, out var warehouseZone))
                    return warehouseZone;

                logger.LogWarning(
                    "Warehouse {WarehouseId} has an unknown time zone {TimeZoneId}, falling back", id, storedId);
            }
        }

        return requestTimeZone.TimeZone ?? TimeZoneInfo.Local;
    }
}
