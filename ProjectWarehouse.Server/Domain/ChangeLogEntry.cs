using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ProjectWarehouse.Server.Domain;

/// <summary>
/// for Npgsql needs EnableDynamicJson
/// </summary>
[Index(nameof(EntityId))]
[Index(nameof(EntityType))]
public class ChangeLogEntry
{
    public Guid Id { get; set; }
    public AppEntityType EntityType { get; set; }
    public Guid EntityId { get; set; }
    public ChangeLogEntryType ChangeLogEntryType { get; set; }
    [Column(TypeName = "jsonb")] public IList<ChangeLogDiff> Diffs { get; set; } = null!;
    [Column(TypeName = "jsonb")] public string? Snapshot { get; set; }
    [Column(TypeName = "jsonb")] public string? Context { get; set; }
    public Guid? UserId { get; set; }

    public ApplicationUser? User { get; set; }
    public DateTime CreatedAt { get; set; }

    public string? Action { get; set; }
    [Column(TypeName = "jsonb")] public string? ActionData { get; set; }
}

public enum ChangeLogEntryType
{
    Added = 1,
    Modified = 2,
    Deleted = 3,
}
