using Npgsql;
using ProjectWarehouse.Server.Data;
using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Infrastructure;
using ProjectWarehouse.Server.Models.System;

namespace ProjectWarehouse.Server.Services;

/// <summary>
/// Table sizes straight out of the Postgres catalog, grouped by <see cref="AppEntityType"/>.
/// Row counts are the planner's estimate rather than COUNT(*): an admin readout is not worth a
/// sequential scan of every table on each page open.
/// </summary>
public class DatabaseStatsService(ApplicationDbContext db, NpgsqlDataSource dataSource) : IDatabaseStatsService
{
    private const string Sql = """
        SELECT c.relname,
               pg_total_relation_size(c.oid)::bigint,
               pg_table_size(c.oid)::bigint,
               pg_indexes_size(c.oid)::bigint,
               c.reltuples::bigint,
               pg_database_size(current_database())::bigint
        FROM pg_class c
        JOIN pg_namespace n ON n.oid = c.relnamespace
        WHERE c.relkind = 'r' AND n.nspname = current_schema()
        """;

    public async Task<DatabaseStatsDto> GetAsync(CancellationToken ct)
    {
        var owners = EntityTypeTables.Resolve(db.Model);
        var tables = new List<(AppEntityType Owner, TableStatDto Stat)>();
        long databaseSize = 0;

        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(Sql, connection);
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            var name = reader.GetString(0);
            var rows = reader.GetInt64(4);
            databaseSize = reader.GetInt64(5);

            tables.Add((
                owners.GetValueOrDefault(name, AppEntityType.Unknown),
                new TableStatDto
                {
                    Name = name,
                    SizeBytes = reader.GetInt64(1),
                    TableSizeBytes = reader.GetInt64(2),
                    IndexSizeBytes = reader.GetInt64(3),
                    // -1 means the table has never been analysed; a made-up zero would read as "empty"
                    RowEstimate = rows < 0 ? null : rows,
                }));
        }

        var byEntityType = tables
            .GroupBy(t => t.Owner)
            .Select(g => new EntityTypeStatDto
            {
                EntityType = g.Key,
                SizeBytes = g.Sum(t => t.Stat.SizeBytes),
                TableSizeBytes = g.Sum(t => t.Stat.TableSizeBytes),
                IndexSizeBytes = g.Sum(t => t.Stat.IndexSizeBytes),
                RowEstimate = g.Any(t => t.Stat.RowEstimate is not null)
                    ? g.Sum(t => t.Stat.RowEstimate ?? 0)
                    : null,
                Tables = g.Select(t => t.Stat).OrderByDescending(t => t.SizeBytes).ToList(),
            })
            .OrderByDescending(x => x.SizeBytes)
            .ToList();

        return new DatabaseStatsDto
        {
            TotalSizeBytes = databaseSize,
            TablesSizeBytes = tables.Sum(t => t.Stat.SizeBytes),
            ByEntityType = byEntityType,
        };
    }
}
