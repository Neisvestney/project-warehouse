using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using ProjectWarehouse.Server.Domain;

namespace ProjectWarehouse.Server.Models.ChangeLog;

public class ChangeLogEntryDto
{
    [Required]
    public Guid Id { get; set; }
    [Required]
    public AppEntityType EntityType { get; set; }
    [Required]
    public Guid EntityId { get; set; }
    [Required]
    public ChangeLogEntryType ChangeLogEntryType { get; set; }
    public IList<ChangeLogDiff> Diffs { get; set; } = null!;
    public string? UserName { get; set; }
    public Guid? UserId { get; set; }
    [Required]
    public DateTime CreatedAt { get; set; }
    public JsonElement? Context { get; set; }
    public string? Action { get; set; }
    public JsonElement? ActionData { get; set; }
}
