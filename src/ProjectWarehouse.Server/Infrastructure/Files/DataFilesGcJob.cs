using Microsoft.Extensions.Options;
using Npgsql;
using ProjectWarehouse.Server.Data;
using ProjectWarehouse.Server.Integrations.Sync;
using Quartz;

namespace ProjectWarehouse.Server.Infrastructure.Files;

/// <summary>
/// Deletes files nothing references. Both kinds of garbage — uploaded but never saved, and
/// dereferenced later — are the same state, so no controller has to call anything on save or
/// delete. There is no such call to forget.
/// </summary>
[DisallowConcurrentExecution]
public class DataFilesGcJob(
    ApplicationDbContext db,
    IFileStorage storage,
    IOptions<DataFilesOptions> options,
    NpgsqlDataSource dataSource,
    ILogger<DataFilesGcJob> logger) : IJob
{
    public const string Key = "data-files-gc";

    public async Task Execute(IJobExecutionContext context)
    {
        var ct = context.CancellationToken;
        var opts = options.Value;

        // Quartz's job store is in-memory, so [DisallowConcurrentExecution] only covers this process.
        // Two instances deleting files at once is exactly what the advisory lock is here to prevent.
        await using var advisoryLock = await PostgresAdvisoryLock.TryAcquireAsync(dataSource, "data-files-gc:", "global", ct);
        if (advisoryLock is null)
        {
            logger.LogInformation("Data files GC skipped: another instance holds the lock");
            return;
        }

        var cutoff = DateTime.UtcNow.AddHours(-opts.OrphanTtlHours);
        var sql = $"""
            DELETE FROM "DataFiles"
            WHERE "Id" IN (
                SELECT f."Id" FROM "DataFiles" f
                WHERE f."CreatedAt" < $1
                  AND {DataFileReferences.BuildOrphanPredicate(db.Model, "f")}
                LIMIT $2
            )
            RETURNING "StorageKey", "SizeBytes";
            """;

        // The row goes first and the bytes second: the reverse order could leave a row pointing at a
        // file that no longer exists, which is a broken image in the UI. This way the worst case is
        // unused bytes on disk.
        var deleted = new List<(string StorageKey, long SizeBytes)>();
        await using (var connection = await dataSource.OpenConnectionAsync(ct))
        await using (var cmd = new NpgsqlCommand(sql, connection))
        {
            cmd.Parameters.AddWithValue(cutoff);
            cmd.Parameters.AddWithValue(opts.GcBatchSize);

            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
                deleted.Add((reader.GetString(0), reader.GetInt64(1)));
        }

        if (deleted.Count == 0) return;

        var failures = 0;
        foreach (var (storageKey, _) in deleted)
        {
            try
            {
                await storage.DeleteAsync(storageKey, ct);
                await storage.DeleteThumbnailsAsync(storageKey, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                failures++;
                logger.LogWarning(ex, "Failed to delete storage key {StorageKey} after its row was removed", storageKey);
            }
        }

        logger.LogInformation(
            "Data files GC removed {RowCount} rows, freed {FreedBytes} bytes, {FailureCount} disk deletions failed",
            deleted.Count, deleted.Sum(x => x.SizeBytes), failures);
    }
}
