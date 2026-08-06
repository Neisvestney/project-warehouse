using System.Security.Cryptography;
using System.Text;
using Npgsql;

namespace ProjectWarehouse.Server.Integrations.Sync;

/// <summary>
/// Session-scoped PostgreSQL advisory lock held on a dedicated connection.
/// It cannot ride on the request's DbContext connection: Npgsql issues DISCARD ALL when returning a
/// pooled connection, which releases advisory locks — the lock would die when the scope ends.
/// </summary>
public sealed class PostgresAdvisoryLock : IAsyncDisposable
{
    private readonly NpgsqlConnection _connection;
    private readonly long _key;

    private PostgresAdvisoryLock(NpgsqlConnection connection, long key)
    {
        _connection = connection;
        _key = key;
    }

    /// <summary>
    /// Marketplace-sync overload. Key-compatible with the pre-generalisation version: during a
    /// rolling deploy old and new instances must hash the same string or both would sync at once.
    /// </summary>
    public static Task<PostgresAdvisoryLock?> TryAcquireAsync(
        NpgsqlDataSource dataSource, Guid resource, CancellationToken ct) =>
        TryAcquireAsync(dataSource, "marketplace-sync:", resource.ToString("N"), ct);

    /// <summary>Returns null when another session already holds the lock.</summary>
    public static async Task<PostgresAdvisoryLock?> TryAcquireAsync(
        NpgsqlDataSource dataSource, string scope, string resource, CancellationToken ct)
    {
        var key = ToKey(scope + resource);
        var connection = await dataSource.OpenConnectionAsync(ct);
        try
        {
            await using var cmd = new NpgsqlCommand("SELECT pg_try_advisory_lock($1)", connection);
            cmd.Parameters.AddWithValue(key);

            if (await cmd.ExecuteScalarAsync(ct) is true)
                return new PostgresAdvisoryLock(connection, key);
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }

        await connection.DisposeAsync();
        return null;
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await using var cmd = new NpgsqlCommand("SELECT pg_advisory_unlock($1)", _connection);
            cmd.Parameters.AddWithValue(_key);
            await cmd.ExecuteScalarAsync();
        }
        catch
        {
            // connection already dead — the lock dies with the session anyway
        }
        finally
        {
            await _connection.DisposeAsync();
        }
    }

    // The advisory key space is per-database, and the scope prefix keeps unrelated users apart.
    private static long ToKey(string resource) =>
        BitConverter.ToInt64(SHA256.HashData(Encoding.UTF8.GetBytes(resource)), 0);
}
