using System.ComponentModel.DataAnnotations;
using ProjectWarehouse.Server.Domain;

namespace ProjectWarehouse.Server.Models.Tags;

public class CreateTagRequest
{
    public TagKind Kind { get; init; }

    [Required, MaxLength(100)]
    public string Name { get; init; } = null!;
}
