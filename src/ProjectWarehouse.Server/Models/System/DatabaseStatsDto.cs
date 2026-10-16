using ProjectWarehouse.Server.Domain;

namespace ProjectWarehouse.Server.Models.System;

public class DatabaseStatsDto
{
    /// <summary>Everything the database occupies, including catalogs — always at least the table sum.</summary>
    public long TotalSizeBytes { get; init; }

    /// <summary>Sum of the groups below. Smaller than <see cref="TotalSizeBytes"/> by system overhead.</summary>
    public long TablesSizeBytes { get; init; }

    public IReadOnlyList<EntityTypeStatDto> ByEntityType { get; init; } = [];
}

public class EntityTypeStatDto
{
    public AppEntityType EntityType { get; init; }
    public long SizeBytes { get; init; }
    public long TableSizeBytes { get; init; }
    public long IndexSizeBytes { get; init; }

    /// <summary>Null when no table in the group has been analysed yet — a zero would read as "empty".</summary>
    public long? RowEstimate { get; init; }

    public IReadOnlyList<TableStatDto> Tables { get; init; } = [];
}

public class TableStatDto
{
    public string Name { get; init; } = null!;

    /// <summary>Table plus TOAST plus indexes — what the table really costs on disk.</summary>
    public long SizeBytes { get; init; }
    public long TableSizeBytes { get; init; }
    public long IndexSizeBytes { get; init; }

    /// <summary>Planner estimate from <c>pg_class.reltuples</c>, not a count. Null before the first ANALYZE.</summary>
    public long? RowEstimate { get; init; }
}
