using Microsoft.Extensions.Options;
using Npgsql;
using ProjectWarehouse.Server.Integrations.Sync;
using Quartz;

namespace ProjectWarehouse.Server.Infrastructure;

/// <summary>
/// Deletes refresh tokens nobody can present any more. Rotation revokes a row on every use, so an active
/// session leaves one dead row behind every access-token lifetime — without this the table only grows.
/// Revoked rows are kept for a grace period rather than dropped on sight: they are the only trace a
/// session leaves, and a support question about "who was logged in yesterday" has nowhere else to look.
/// </summary>
[DisallowConcurrentExecution]
public class RefreshTokensGcJob(
    NpgsqlDataSource dataSource,
    IOptions<JwtOptions> options,
    ILogger<RefreshTokensGcJob> logger) : IJob
{
    public const string Key = "refresh-tokens-gc";

    public async Task Execute(IJobExecutionContext context)
    {
        var ct = context.CancellationToken;
        var jwt = options.Value;

        // Quartz's job store is in-memory, so [DisallowConcurrentExecution] only covers this process.
        await using var advisoryLock = await PostgresAdvisoryLock.TryAcquireAsync(
            dataSource, "refresh-tokens-gc:", "global", ct);
        if (advisoryLock is null)
        {
            logger.LogInformation("Refresh tokens GC skipped: another instance holds the lock");
            return;
        }

        var batchSize = jwt.RefreshTokenGcBatchSize;

        // Expiry is what makes a row unusable, revoked or not, so one cutoff covers both kinds of garbage.
        var cutoff = DateTime.UtcNow.AddDays(-jwt.RefreshTokenRetentionDays);

        // Batched: rotation leaves a row per access-token lifetime per session, so the first run after this
        // job is introduced faces the whole accumulated backlog. One statement over all of it would sit in a
        // single transaction and can outlive the command timeout, and a run that always times out never
        // makes progress.
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        var total = 0;
        int removed;
        do
        {
            await using var cmd = new NpgsqlCommand(
                """
                DELETE FROM "RefreshTokens"
                WHERE "Id" IN (SELECT "Id" FROM "RefreshTokens" WHERE "ExpiresAt" < $1 LIMIT $2);
                """, connection);
            cmd.Parameters.AddWithValue(cutoff);
            cmd.Parameters.AddWithValue(batchSize);

            removed = await cmd.ExecuteNonQueryAsync(ct);
            total += removed;
        } while (removed == batchSize && !ct.IsCancellationRequested);

        if (total > 0)
            logger.LogInformation("Refresh tokens GC removed {RowCount} expired rows", total);
    }
}
