using ProjectWarehouse.Server.Models.System;

namespace ProjectWarehouse.Server.Services;

public interface IStorageStatsService
{
    Task<StorageStatsDto> GetAsync(CancellationToken ct);
}
