using System.ComponentModel.DataAnnotations;

namespace ProjectWarehouse.Server.Models.Tags;

public class RenameTagRequest
{
    [Required, MaxLength(100)]
    public string Name { get; init; } = null!;
}
