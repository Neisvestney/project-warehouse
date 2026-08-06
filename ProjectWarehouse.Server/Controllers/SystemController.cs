using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectWarehouse.Server.Infrastructure;
using ProjectWarehouse.Server.Models.System;
using ProjectWarehouse.Server.Services;

namespace ProjectWarehouse.Server.Controllers;

/// <summary>
/// Instance-wide technical readouts. Not named StorageController: "storage" already means warehouse
/// storage places everywhere else in this codebase.
/// </summary>
[Route("api/system")]
public class SystemController(
    IStorageStatsService storageStats,
    IDatabaseStatsService databaseStats) : AppControllerBase
{
    /// <summary>File storage usage: counts, sizes, orphans and free disk space.</summary>
    /// <remarks>Disk figures are cached for DataFiles:StatsCacheSeconds; see <c>diskStatsAt</c>.</remarks>
    [HttpGet("storage")]
    [Authorize(Policy = Permissions.System.View)]
    [ProducesResponseType<StorageStatsDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStorageStats(CancellationToken ct = default) =>
        Ok(await storageStats.GetAsync(ct));

    /// <summary>Database size broken down by the entity type each table belongs to.</summary>
    /// <remarks>Row counts are planner estimates from pg_class, not exact counts.</remarks>
    [HttpGet("database")]
    [Authorize(Policy = Permissions.System.View)]
    [ProducesResponseType<DatabaseStatsDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDatabaseStats(CancellationToken ct = default) =>
        Ok(await databaseStats.GetAsync(ct));
}
