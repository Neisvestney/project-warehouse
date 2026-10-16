using ProjectWarehouse.Server.Models.System;

namespace ProjectWarehouse.Server.Services;

public interface IDatabaseStatsService
{
    Task<DatabaseStatsDto> GetAsync(CancellationToken ct);
}
