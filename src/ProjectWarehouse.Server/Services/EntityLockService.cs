using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using ProjectWarehouse.Server.Data;
using ProjectWarehouse.Server.Infrastructure;
using ProjectWarehouse.Server.Integrations.Sync;

namespace ProjectWarehouse.Server.Services;

public class EntityLockService(ApplicationDbContext db) : IEntityLockService
{
    public Task LockAsync<TEntity>(Guid id, CancellationToken ct, TimeSpan? timeout = null, bool forDelete = false)
        where TEntity : class =>
        LockRowsAsync(typeof(TEntity), [id], ct, timeout, forDelete);

    public Task LockManyAsync<TEntity>(IEnumerable<Guid> ids, CancellationToken ct, TimeSpan? timeout = null)
        where TEntity : class =>
        LockRowsAsync(typeof(TEntity), ids.Distinct().ToArray(), ct, timeout, forDelete: false);

    public async Task LockKeyAsync(string scope, string key, CancellationToken ct, TimeSpan? timeout = null)
    {
        var resource = $"{scope}:{key}";
        await RunLockedAsync(resource, timeout, () =>
            db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock({0})",
                [PostgresAdvisoryLock.ToKey(resource)], ct), ct);
    }

    public async Task LockKeysAsync(string scope, IEnumerable<string> keys, CancellationToken ct,
        TimeSpan? timeout = null)
    {
        var hashes = keys.Select(k => PostgresAdvisoryLock.ToKey($"{scope}:{k}")).Distinct().Order().ToArray();
        if (hashes.Length == 0) return;

        // a function scan over unnest yields the array in order, so the locks follow the sorted keys
        await RunLockedAsync($"{scope}:{hashes.Length} keys", timeout, () =>
            db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(k) FROM unnest({0}) AS k", [hashes], ct), ct);
    }

    private async Task LockRowsAsync(Type entityType, Guid[] ids, CancellationToken ct, TimeSpan? timeout,
        bool forDelete)
    {
        if (ids.Length == 0) return;

        var entity = db.Model.FindEntityType(entityType)
            ?? throw new InvalidOperationException($"'{entityType.Name}' is not an entity of the model.");
        var table = entity.GetTableName()
            ?? throw new InvalidOperationException($"'{entityType.Name}' is not mapped to a table.");
        if (entity.FindPrimaryKey()?.Properties is not [var keyProperty] || keyProperty.ClrType != typeof(Guid))
            throw new InvalidOperationException($"'{entityType.Name}' has no single Guid primary key.");
        var key = keyProperty.GetColumnName(StoreObjectIdentifier.Table(table, entity.GetSchema()))!;

        var sql = db.GetService<ISqlGenerationHelper>();
        var tableSql = sql.DelimitIdentifier(table, entity.GetSchema());
        var keySql = sql.DelimitIdentifier(key);

        // ORDER BY sits under the lock step, so rows are locked in id order and overlapping batches cannot deadlock
        await RunLockedAsync($"{table}:{(ids.Length == 1 ? ids[0] : $"{ids.Length} rows")}", timeout, () =>
            db.Database.ExecuteSqlRawAsync(
                $"SELECT 1 FROM {tableSql} WHERE {keySql} = ANY({{0}}) ORDER BY {keySql} {(forDelete ? "FOR UPDATE" : "FOR NO KEY UPDATE")}",
                [ids], ct), ct);
    }

    private async Task RunLockedAsync(string resource, TimeSpan? timeout, Func<Task> acquire, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException(
                $"Lock on '{resource}' requested outside a transaction; it would be released immediately.");

        var ms = (int)(timeout ?? IEntityLockService.DefaultTimeout).TotalMilliseconds;
        await db.Database.ExecuteSqlRawAsync("SELECT set_config('lock_timeout', {0}, true)", [$"{ms}ms"], ct);

        try
        {
            await acquire();
        }
        catch (PostgresException e) when (e.SqlState == PostgresErrorCodes.LockNotAvailable)
        {
            throw new EntityLockedException(resource, e);
        }

        // the timeout is for this lock only; any later wait in the action would surface as a bare 55P03
        await db.Database.ExecuteSqlRawAsync("RESET lock_timeout", ct);
    }
}
