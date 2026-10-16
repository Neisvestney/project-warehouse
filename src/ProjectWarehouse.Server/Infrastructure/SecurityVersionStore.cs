using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using ProjectWarehouse.Server.Data;

namespace ProjectWarehouse.Server.Infrastructure;

/// <summary>
/// In-process cache of SecurityVersion values. Backed by DB for persistence across restarts.
/// NOT suitable for multi-instance deployments — replace with a distributed cache (Redis etc.)
/// if more than one app instance runs concurrently.
/// </summary>
public sealed class SecurityVersionStore(IServiceScopeFactory scopeFactory)
{
    /// <summary>
    /// Returned for a user that no longer exists. It must not collide with any value a token can carry —
    /// <c>0</c> would, and a deleted user whose version was never bumped would keep a working token.
    /// It is never cached: a missing row is cheap to re-read and the caller is being rejected anyway.
    /// </summary>
    public const int NoSuchUser = -1;

    private readonly ConcurrentDictionary<Guid, int> _versions = new();

    // Bumped on every invalidation. A read that started before an invalidation must not publish the value it
    // fetched, or a bump landing mid-read would be cached away and the revocation lost until the next one.
    private long _generation;

    public async Task<int> GetVersionAsync(Guid userId)
    {
        if (_versions.TryGetValue(userId, out var cached))
            return cached;

        var generation = Interlocked.Read(ref _generation);

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = await db.Users.FindAsync(userId);
        if (user is null) return NoSuchUser;

        if (Interlocked.Read(ref _generation) == generation)
            _versions[userId] = user.SecurityVersion;

        return user.SecurityVersion;
    }

    public async Task BumpAsync(Guid userId)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Users
            .Where(u => u.Id == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.SecurityVersion, u => u.SecurityVersion + 1));
        Evict(userId);
    }

    public void Evict(Guid userId)
    {
        Interlocked.Increment(ref _generation);
        _versions.TryRemove(userId, out _);
    }
}
