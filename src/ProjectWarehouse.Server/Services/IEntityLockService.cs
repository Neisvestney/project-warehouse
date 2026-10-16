namespace ProjectWarehouse.Server.Services;

/// <summary>
/// Pessimistic locks held until the current transaction commits or rolls back. Every method throws when
/// no transaction is open, and turns a lock not granted within the timeout into
/// <see cref="Infrastructure.EntityLockedException" />.
/// </summary>
public interface IEntityLockService
{
    static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(5);

    /// <summary>Background sync has no user waiting on it and outlasts a user's request rather than failing a run.</summary>
    static readonly TimeSpan BackgroundTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Row lock (<c>FOR NO KEY UPDATE</c>) on an existing entity; a missing row locks nothing.
    /// <paramref name="forDelete" /> takes <c>FOR UPDATE</c> instead, which also conflicts with the
    /// <c>FOR KEY SHARE</c> a child insert takes on its parent — a "has no children" check before a delete then
    /// holds until the commit.
    /// </summary>
    Task LockAsync<TEntity>(Guid id, CancellationToken ct, TimeSpan? timeout = null, bool forDelete = false)
        where TEntity : class;

    /// <summary>Locks the rows in id order, so two overlapping batches cannot deadlock each other.</summary>
    Task LockManyAsync<TEntity>(IEnumerable<Guid> ids, CancellationToken ct, TimeSpan? timeout = null)
        where TEntity : class;

    /// <summary>
    /// Transaction-scoped advisory lock on a resource that has no row to lock yet — an entity about to be
    /// created, or a logical resource spanning rows.
    /// </summary>
    Task LockKeyAsync(string scope, string key, CancellationToken ct, TimeSpan? timeout = null);

    /// <summary>Several advisory keys in one statement, taken in key order so overlapping sets cannot deadlock.</summary>
    Task LockKeysAsync(string scope, IEnumerable<string> keys, CancellationToken ct, TimeSpan? timeout = null);
}
