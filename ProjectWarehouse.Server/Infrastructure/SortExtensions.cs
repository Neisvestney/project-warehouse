using System.Globalization;
using System.Linq.Expressions;
using EntityFrameworkCore.Projectables;
using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Models;

namespace ProjectWarehouse.Server.Infrastructure;

public static class SortExtensions
{
    public static IOrderedQueryable<T> Sort<T, TKey>(
        this IQueryable<T> query,
        Expression<Func<T, TKey>> keySelector,
        SortOrder sortOrder) =>
        sortOrder == SortOrder.Asc
            ? query.OrderBy(keySelector)
            : query.OrderByDescending(keySelector);

    public static IOrderedQueryable<T> ThenSort<T, TKey>(
        this IOrderedQueryable<T> query,
        Expression<Func<T, TKey>> keySelector,
        SortOrder sortOrder) =>
        sortOrder == SortOrder.Asc
            ? query.ThenBy(keySelector)
            : query.ThenByDescending(keySelector);

    public static IOrderedQueryable<CatalogItem> OrderByCatalog(this IQueryable<CatalogItem> query) =>
        query.OrderBy(c => c.IsArchived).ThenBy(c => c.FullName).ThenBy(c => c.Id);

    // Projectable so it can be used inside a filtered Include
    [Projectable]
    public static IOrderedEnumerable<CatalogItemVariationMember> InCatalogOrder(
        this IEnumerable<CatalogItemVariationMember> members) =>
        members.OrderBy(m => m.Item.IsArchived).ThenBy(m => m.Item.FullName).ThenBy(m => m.ItemId);

    /// <summary>In-memory counterpart of the catalog list's default order, for rows already loaded.</summary>
    public static readonly StringComparer CatalogNameComparer = StringComparer.InvariantCulture;

    /// <summary>"2" before "10" — matches the client's <c>localeCompare(..., {numeric: true})</c>.</summary>
    public static readonly StringComparer InventoryNumberComparer =
        StringComparer.Create(CultureInfo.InvariantCulture, CompareOptions.NumericOrdering);

    public static IOrderedEnumerable<T> OrderLikeCatalog<T>(this IEnumerable<T> source, Func<T, CatalogItem?> item) =>
        source.OrderBy(x => item(x)?.IsArchived ?? false).ThenBy(x => item(x)?.FullName ?? "", CatalogNameComparer);
}
