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

    public static IQueryable<T> WhereMatchesSearch<T>(
        this IQueryable<T> query,
        Expression<Func<T, string>> searchField,
        string? searchString)
    {
        if (string.IsNullOrWhiteSpace(searchString))
            return query;

        var param = searchField.Parameters[0];
        var escapeChar = Expression.Constant(EscapeChar);
        var normalizedField = Expression.Call(NormalizeMethod, searchField.Body);

        foreach (var token in Tokenize(searchString))
        {
            var pattern = Expression.Constant(ToPattern(token));
            var call = Expression.Call(ILikeMethod, EfFunctionsExpr, normalizedField, pattern, escapeChar);
            query = query.Where(Expression.Lambda<Func<T, bool>>(call, param));
        }

        return query;
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
