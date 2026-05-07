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
    private readonly ConcurrentDictionary<Guid, int> _versions = new();

    public async Task<int> GetVersionAsync(Guid userId)
    {
        if (_versions.TryGetValue(userId, out var cached))
            return cached;

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = await db.Users.FindAsync(userId);
        var version = user?.SecurityVersion ?? 0;
        _versions[userId] = version;
        return version;
    }

    public async Task BumpAsync(Guid userId)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Users
            .Where(u => u.Id == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.SecurityVersion, u => u.SecurityVersion + 1));
        _versions.TryRemove(userId, out _);
    }

    public void Evict(Guid userId) => _versions.TryRemove(userId, out _);
}
