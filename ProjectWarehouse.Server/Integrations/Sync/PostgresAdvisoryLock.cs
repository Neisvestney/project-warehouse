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

    /// <summary>Returns null when another session already holds the lock.</summary>
    public static async Task<PostgresAdvisoryLock?> TryAcquireAsync(NpgsqlDataSource dataSource, Guid resource, CancellationToken ct)
    {
        var key = ToKey(resource);
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

    // The advisory key space is per-database and this module is its only user, so a plain hash is safe.
    private static long ToKey(Guid resource)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes("marketplace-sync:" + resource.ToString("N")));
        return BitConverter.ToInt64(hash, 0);
    }
}
