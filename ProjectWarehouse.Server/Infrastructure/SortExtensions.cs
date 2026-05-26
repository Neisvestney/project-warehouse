using System.Linq.Expressions;
using ProjectWarehouse.Server.Models;

namespace ProjectWarehouse.Server.Infrastructure;

public static class SortExtensions
{
    public static IOrderedQueryable<T> ThenSort<T, TKey>(
        this IOrderedQueryable<T> query,
        Expression<Func<T, TKey>> keySelector,
        SortOrder sortOrder) =>
        sortOrder == SortOrder.Asc
            ? query.ThenBy(keySelector)
            : query.ThenByDescending(keySelector);
}
