using Microsoft.EntityFrameworkCore;

namespace ProjectWarehouse.Server.Models;

public class Paginated<T>
{
    public IReadOnlyList<T> Items { get; init; } = [];
    public int Total { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalPages => (int)Math.Ceiling((double)Total / PageSize);
    public bool HasNextPage => Page < TotalPages;
    public bool HasPreviousPage => Page > 1;
}

public static class PaginatedExtensions
{
    public static async Task<Paginated<T>> ToPaginatedAsync<T>(
        this IQueryable<T> query,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(page, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        return new Paginated<T>
        {
            Items = items,
            Total = total,
            Page = page,
            PageSize = pageSize,
        };
    }

    public static PaginatedWithMeta<T, TMeta> WithMeta<T, TMeta>(this Paginated<T> paginated, TMeta meta)
        where TMeta : notnull =>
        new()
        {
            Items = paginated.Items,
            Total = paginated.Total,
            Page = paginated.Page,
            PageSize = paginated.PageSize,
            Meta = meta,
        };
}

/// <summary>A page plus aggregates computed over the whole filtered set, not just the page.</summary>
public class PaginatedWithMeta<T, TMeta> : Paginated<T> where TMeta : notnull
{
    public required TMeta Meta { get; init; }
}
