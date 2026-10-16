using System.Linq.Expressions;
using System.Security.Claims;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Infrastructure;
using ProjectWarehouse.Server.Models;

namespace ProjectWarehouse.Server.Services;

/// <summary>
/// One <c>UNION ALL</c> over every searchable type yields ranked <c>(type, id)</c> hits; only the picked hits are
/// then loaded as <see cref="AppEntity"/>, since a per-type <c>AdditionalFields</c> projection cannot sit in a union.
/// </summary>
public class GlobalSearchService(IMapper mapper, IUserQueryFilterService queryFilter) : IGlobalSearchService
{
    private const int Limit = 10;
    private const int RecentOrdersFuzzyCap = 1000;

    public async Task<List<AppEntity>> SearchAsync(ClaimsPrincipal user, string searchString, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(searchString)) return [];

        var normalized = SearchExtensions.Normalize(searchString.Trim());
        var fuzzyQuery = normalized.Length >= SearchExtensions.FuzzyMinLength ? normalized : null;

        SearchSource[] sources =
        [
            new SearchSource<Order>(AppEntityType.Order, await queryFilter.GetOrdersAsync(user, ct),
                x => x.Id, x => x.SearchString,
                fuzzyScope: q => q.OrderByDescending(x => x.Number).Take(RecentOrdersFuzzyCap)),
            new SearchSource<Warehouse>(AppEntityType.Warehouse, await queryFilter.GetWarehousesAsync(user, ct),
                x => x.Id, x => x.SearchString),
            new SearchSource<Receipt>(AppEntityType.Receipt, await queryFilter.GetReceiptsAsync(user, ct),
                x => x.Id, x => x.SearchString),
            new SearchSource<CatalogItem>(AppEntityType.CatalogItem, await queryFilter.GetCatalogItemsAsync(user, ct),
                x => x.Id, x => x.SearchString),
            new SearchSource<MarketplaceAccount>(AppEntityType.MarketplaceAccount,
                await queryFilter.GetMarketplaceAccountsAsync(user, ct), x => x.Id, x => x.SearchString),
            new SearchSource<Organization>(AppEntityType.Organization, await queryFilter.GetOrganizationsAsync(user, ct),
                x => x.Id, x => x.SearchString),
            new SearchSource<ApplicationUser>(AppEntityType.User, await queryFilter.GetUsersAsync(user, ct),
                x => x.Id, x => x.SearchString),
            new SearchSource<Stocktake>(AppEntityType.Stocktake, await queryFilter.GetStocktakesAsync(user, ct),
                x => x.Id, x => x.SearchString),
            new SearchSource<Writeoff>(AppEntityType.Writeoff, await queryFilter.GetWriteoffsAsync(user, ct),
                x => x.Id, x => x.SearchString),
        ];

        var hits = await sources
            .Select(s => s.Hits(searchString, normalized, fuzzyQuery))
            .Aggregate((a, b) => a.Concat(b))
            .ToListAsync(ct);

        var picked = PickWithPerTypeMinimum(Rank(hits), Limit);

        var loaded = new Dictionary<(AppEntityType, Guid), AppEntity>();
        foreach (var source in sources)
        {
            var ids = picked.Where(h => h.Type == source.Type).Select(h => h.Id).ToList();
            if (ids.Count == 0) continue;
            foreach (var entity in await source.LoadAsync(ids, mapper, ct))
                loaded[(source.Type, entity.Id!.Value)] = entity;
        }

        // A hit can vanish between the two queries if the row was deleted meanwhile.
        return picked
            .Select(h => loaded.GetValueOrDefault((h.Type, h.Id)))
            .OfType<AppEntity>()
            .ToList();
    }

    /// <summary>A row found by both branches keeps its exact hit; exact hits outrank fuzzy ones.</summary>
    private static List<SearchHit> Rank(IEnumerable<SearchHit> hits) =>
        hits
            .GroupBy(h => (h.Type, h.Id))
            .Select(g => g.OrderByDescending(h => h.Exact).ThenByDescending(h => h.Score).First())
            .OrderByDescending(h => h.Exact)
            .ThenByDescending(h => h.Score)
            .ThenBy(h => h.Id)
            .ToList();

    /// <summary>The best hit of every type gets a slot first, the rest go by rank; the result keeps rank order.</summary>
    private static List<SearchHit> PickWithPerTypeMinimum(List<SearchHit> ranked, int limit)
    {
        var picked = ranked.GroupBy(h => h.Type).Select(g => g.First()).Take(limit).ToHashSet();
        foreach (var hit in ranked)
        {
            if (picked.Count >= limit) break;
            picked.Add(hit);
        }

        return ranked.Where(picked.Contains).ToList();
    }

    private sealed class SearchHit
    {
        public AppEntityType Type { get; init; }
        public Guid Id { get; init; }
        public bool Exact { get; init; }
        public double Score { get; init; }
    }

    private abstract class SearchSource
    {
        public abstract AppEntityType Type { get; }

        public abstract IQueryable<SearchHit> Hits(string searchString, string normalized, string? fuzzyQuery);

        public abstract Task<List<AppEntity>> LoadAsync(List<Guid> ids, IMapper mapper, CancellationToken ct);
    }

    // fuzzyScope narrows only the rows the fuzzy branch scans; the exact branch always scans all visible rows.
    private sealed class SearchSource<T>(
        AppEntityType type,
        IQueryable<T> visible,
        Expression<Func<T, Guid>> id,
        Expression<Func<T, string>> text,
        Func<IQueryable<T>, IQueryable<T>>? fuzzyScope = null) : SearchSource where T : class
    {
        private readonly ParameterExpression _row = text.Parameters[0];
        private readonly Expression _id = ReplacingExpressionVisitor.Replace(id.Parameters[0], text.Parameters[0], id.Body);

        public override AppEntityType Type => type;

        public override IQueryable<SearchHit> Hits(string searchString, string normalized, string? fuzzyQuery)
        {
            var hits = visible
                .WhereMatchesSearch(text, searchString)
                .Select(Project(normalized, exact: true))
                .OrderByDescending(h => h.Score)
                .ThenBy(h => h.Id)
                .Take(Limit);

            if (fuzzyQuery is null) return hits;

            var fuzzy = (fuzzyScope?.Invoke(visible) ?? visible)
                .Where(OverText(s => EF.Functions.TrigramsAreWordSimilar(fuzzyQuery, SearchExtensions.Normalize(s))))
                .Select(Project(normalized, exact: false))
                .OrderByDescending(h => h.Score)
                .ThenBy(h => h.Id)
                .Take(Limit);

            return hits.Concat(fuzzy);
        }

        public override Task<List<AppEntity>> LoadAsync(List<Guid> ids, IMapper mapper, CancellationToken ct)
        {
            Expression<Func<Guid, bool>> inIds = x => ids.Contains(x);
            var predicate = Expression.Lambda<Func<T, bool>>(
                ReplacingExpressionVisitor.Replace(inIds.Parameters[0], _id, inIds.Body), _row);

            return visible.Where(predicate).ProjectTo<AppEntity>(mapper.ConfigurationProvider).ToListAsync(ct);
        }

        private Expression<Func<T, SearchHit>> Project(string normalized, bool exact)
        {
            Expression<Func<string, double>> score =
                s => EF.Functions.TrigramsWordSimilarity(normalized, SearchExtensions.Normalize(s));

            var init = Expression.MemberInit(
                Expression.New(typeof(SearchHit)),
                Expression.Bind(typeof(SearchHit).GetProperty(nameof(SearchHit.Type))!, Expression.Constant(type)),
                Expression.Bind(typeof(SearchHit).GetProperty(nameof(SearchHit.Id))!, _id),
                Expression.Bind(typeof(SearchHit).GetProperty(nameof(SearchHit.Exact))!, Expression.Constant(exact)),
                Expression.Bind(typeof(SearchHit).GetProperty(nameof(SearchHit.Score))!, Substitute(score)));

            return Expression.Lambda<Func<T, SearchHit>>(init, _row);
        }

        private Expression<Func<T, bool>> OverText(Expression<Func<string, bool>> predicate) =>
            Expression.Lambda<Func<T, bool>>(Substitute(predicate), _row);

        private Expression Substitute<TResult>(Expression<Func<string, TResult>> overText) =>
            ReplacingExpressionVisitor.Replace(overText.Parameters[0], text.Body, overText.Body);
    }
}
