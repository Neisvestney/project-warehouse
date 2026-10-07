using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore;

namespace ProjectWarehouse.Server.Infrastructure;

public static class SearchExtensions
{
    public const string EscapeChar = @"\";

    // Cyrillic letters folded onto Latin look-alikes; lowercase pairs are kept so ILIKE stays case-consistent.
    public const string HomoglyphsFrom = "АаВвЕеКкМмНнОоРрСсТтУуХх";
    public const string HomoglyphsTo = "AaBbEeKkMmHhOoPpCcTtYyXx";

    public static readonly MethodInfo NormalizeMethod = typeof(SearchExtensions).GetMethod(nameof(Normalize))!;

    private static readonly MethodInfo ILikeMethod =
        ((MethodCallExpression)((Expression<Func<bool>>)(() => EF.Functions.ILike("", "", ""))).Body).Method;

    private static readonly Expression EfFunctionsExpr = Expression.Constant(EF.Functions);

    // Shorter queries make pg_trgm word similarity match almost anything.
    public const int FuzzyMinLength = 3;

    /// <summary>
    /// Every token must be a substring of the field. With <paramref name="fuzzy"/> a row also passes when the whole
    /// query is trigram-word-similar to it (pg_trgm <c>&lt;%</c>), for queries of at least <see cref="FuzzyMinLength"/> chars.
    /// </summary>
    public static IQueryable<T> WhereMatchesSearch<T>(
        this IQueryable<T> query,
        Expression<Func<T, string>> searchField,
        string? searchString,
        bool fuzzy = false)
    {
        if (string.IsNullOrWhiteSpace(searchString))
            return query;

        var body = AllTokensMatch(searchField, searchString);
        var normalizedQuery = Normalize(searchString.Trim());
        if (fuzzy && normalizedQuery.Length >= FuzzyMinLength)
            body = Expression.OrElse(body,
                OverField(searchField, s => EF.Functions.TrigramsAreWordSimilar(normalizedQuery, Normalize(s))));

        return query.Where(Expression.Lambda<Func<T, bool>>(body, searchField.Parameters[0]));
    }

    /// <summary>
    /// Substring matches first, then by trigram word similarity to the query, then by <paramref name="thenBy"/>.
    /// Without a search it is plain <paramref name="thenBy"/>.
    /// </summary>
    public static IOrderedQueryable<T> OrderBySearchRelevance<T, TKey>(
        this IQueryable<T> query,
        Expression<Func<T, string>> searchField,
        string? searchString,
        Expression<Func<T, TKey>> thenBy)
    {
        if (string.IsNullOrWhiteSpace(searchString))
            return query.OrderBy(thenBy);

        var (exact, score) = RelevanceKeys(searchField, searchString);
        return query.OrderByDescending(exact).ThenByDescending(score).ThenBy(thenBy);
    }

    /// <summary>Same keys as <see cref="OrderBySearchRelevance{T,TKey}"/>, appended to an existing order; no-op without a search.</summary>
    public static IOrderedQueryable<T> ThenBySearchRelevance<T>(
        this IOrderedQueryable<T> query,
        Expression<Func<T, string>> searchField,
        string? searchString)
    {
        if (string.IsNullOrWhiteSpace(searchString))
            return query;

        var (exact, score) = RelevanceKeys(searchField, searchString);
        return query.ThenByDescending(exact).ThenByDescending(score);
    }

    private static (Expression<Func<T, bool>> Exact, Expression<Func<T, double>> Score) RelevanceKeys<T>(
        Expression<Func<T, string>> searchField,
        string searchString)
    {
        var param = searchField.Parameters[0];
        var normalizedQuery = Normalize(searchString.Trim());
        return (
            Expression.Lambda<Func<T, bool>>(AllTokensMatch(searchField, searchString), param),
            Expression.Lambda<Func<T, double>>(
                OverField(searchField, s => EF.Functions.TrigramsWordSimilarity(normalizedQuery, Normalize(s))), param));
    }

    /// <summary>
    /// Same token semantics as <see cref="WhereMatchesSearch{T}"/> — every token must match — but the match
    /// itself is a caller-supplied predicate over one ready-made ILIKE pattern, so it can span collections.
    /// The pattern is already homoglyph-folded, so the predicate must wrap every column in <see cref="Normalize"/>.
    /// </summary>
    public static IQueryable<T> WhereMatchesExtendedSearch<T>(
        this IQueryable<T> query,
        Expression<Func<T, string, bool>> matches,
        string? searchString)
    {
        if (string.IsNullOrWhiteSpace(searchString))
            return query;

        var entity = matches.Parameters[0];
        var patternParam = matches.Parameters[1];

        foreach (var token in Tokenize(searchString))
        {
            var body = new ParameterReplacer(patternParam, Expression.Constant(ToPattern(token))).Visit(matches.Body);
            query = query.Where(Expression.Lambda<Func<T, bool>>(body, entity));
        }

        return query;
    }

    /// <summary>
    /// Folds Cyrillic look-alike letters onto Latin ones. Translated to SQL <c>translate()</c> in
    /// <c>ApplicationDbContext</c>; the body only runs on client evaluation.
    /// </summary>
    public static string Normalize(string value)
    {
        var chars = value.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            var idx = HomoglyphsFrom.IndexOf(chars[i]);
            if (idx >= 0)
                chars[i] = HomoglyphsTo[idx];
        }

        return new string(chars);
    }

    private static Expression AllTokensMatch<T>(Expression<Func<T, string>> searchField, string searchString)
    {
        var normalizedField = Expression.Call(NormalizeMethod, searchField.Body);
        return Tokenize(searchString)
            .Select(token => (Expression)Expression.Call(ILikeMethod, EfFunctionsExpr, normalizedField,
                Expression.Constant(ToPattern(token)), Expression.Constant(EscapeChar)))
            .Aggregate(Expression.AndAlso);
    }

    private static Expression OverField<T, TResult>(
        Expression<Func<T, string>> searchField,
        Expression<Func<string, TResult>> template) =>
        new ParameterReplacer(template.Parameters[0], searchField.Body).Visit(template.Body);

    private static IEnumerable<string> Tokenize(string searchString) =>
        searchString.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);

    private static string ToPattern(string token) =>
        $"%{Normalize(token).Replace(EscapeChar, EscapeChar + EscapeChar).Replace("%", @"\%").Replace("_", @"\_")}%";

    private sealed class ParameterReplacer(ParameterExpression from, Expression to) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) =>
            node == from ? to : base.VisitParameter(node);
    }
}
