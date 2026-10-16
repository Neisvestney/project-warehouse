using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace ProjectWarehouse.Server.Models;

/// <summary>How many documents of a filtered list sit in one status.</summary>
public class StatusCountDto<TStatus> where TStatus : struct, Enum
{
    public TStatus Status { get; init; }
    public int Count { get; init; }
}

/// <summary>Aggregates of a document list whose only facet is its status tabs.</summary>
public class StatusListMetaDto<TStatus> where TStatus : struct, Enum
{
    /// <summary>
    /// Document count per status, one entry for every status. Ignores the <c>status</c> filter, so the list tabs
    /// show what each of them would hold.
    /// </summary>
    public IReadOnlyList<StatusCountDto<TStatus>> StatusCounts { get; init; } = [];
}

public static class StatusCountExtensions
{
    /// <summary>
    /// Counts <paramref name="query"/> per status in one grouping and fills in zeros, so every status of the enum
    /// is present. Pass the query without its own status filter.
    /// </summary>
    public static async Task<IReadOnlyList<StatusCountDto<TStatus>>> CountByStatusAsync<TEntity, TStatus>(
        this IQueryable<TEntity> query,
        Expression<Func<TEntity, TStatus>> status,
        CancellationToken cancellationToken = default)
        where TStatus : struct, Enum
    {
        var counts = await query
            .GroupBy(status)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.Key, g => g.Count, cancellationToken);

        return Enum.GetValues<TStatus>()
            .Select(s => new StatusCountDto<TStatus> { Status = s, Count = counts.GetValueOrDefault(s) })
            .ToList();
    }
}
