using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using ProjectWarehouse.Server.Domain;

namespace ProjectWarehouse.Server.Infrastructure.Files;

/// <summary>
/// Finds every place that references a <see cref="DataFile"/> by reading the EF model at runtime.
/// Nothing here is maintained by hand, so registering a new attachment point cannot be forgotten:
/// adding the foreign key is the registration.
/// </summary>
public static class DataFileReferences
{
    private static readonly ConcurrentDictionary<IModel, IReadOnlyList<(string Table, string Column)>> Cache = new();

    public static IReadOnlyList<(string Table, string Column)> GetReferencingColumns(IModel model) =>
        Cache.GetOrAdd(model, static m => m.GetEntityTypes()
            .SelectMany(t => t.GetForeignKeys())
            .Where(fk => fk.PrincipalEntityType.ClrType == typeof(DataFile) && fk.Properties.Count == 1)
            .Select(fk =>
            {
                var declaring = fk.DeclaringEntityType;
                var table = StoreObjectIdentifier.Table(declaring.GetTableName()!, declaring.GetSchema());
                return (Table: declaring.GetTableName()!, Column: fk.Properties[0].GetColumnName(table)!);
            })
            // TPH maps several entity types onto one table and would otherwise duplicate conditions
            .Distinct()
            .ToList());

    /// <summary>
    /// SQL predicate that is true for rows nothing points at, e.g.
    /// <c>NOT EXISTS (SELECT 1 FROM "CatalogItems" x WHERE x."MainImageFileId" = f."Id") AND ...</c>
    /// </summary>
    /// <remarks>
    /// Identifiers come from the model rather than from user input, but they still have to be
    /// double-quoted: Postgres folds unquoted names to lower case and this schema is PascalCase.
    /// </remarks>
    public static string BuildOrphanPredicate(IModel model, string alias)
    {
        var columns = GetReferencingColumns(model);
        if (columns.Count == 0) return "TRUE";

        return string.Join(" AND ", columns.Select(c =>
            $"""NOT EXISTS (SELECT 1 FROM "{c.Table}" x WHERE x."{c.Column}" = {alias}."Id")"""));
    }
}
