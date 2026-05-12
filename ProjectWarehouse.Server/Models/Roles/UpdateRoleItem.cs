using System.ComponentModel.DataAnnotations;

namespace ProjectWarehouse.Server.Models.Roles;

public class UpdateRoleItem
{
    public Guid? Id { get; init; }

    [Required, MinLength(1)]
    public string Name { get; init; } = null!;

    public IReadOnlyList<string> Permissions { get; init; } = [];
}
