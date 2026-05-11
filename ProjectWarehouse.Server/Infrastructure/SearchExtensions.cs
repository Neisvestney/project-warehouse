using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore;

namespace ProjectWarehouse.Server.Infrastructure;

public static class SearchExtensions
{
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
        var escapeChar = Expression.Constant(@"\");

        foreach (var token in searchString.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var escaped = token.Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_");
            var pattern = Expression.Constant($"%{escaped}%");
            var call = Expression.Call(ILikeMethod, EfFunctionsExpr, searchField.Body, pattern, escapeChar);
            query = query.Where(Expression.Lambda<Func<T, bool>>(call, param));
        }

        return query;
    }
}
