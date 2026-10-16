using System.ComponentModel.DataAnnotations;
using ProjectWarehouse.Server.Infrastructure;

namespace ProjectWarehouse.Server.Models.Roles;

public class UpdateRoleItem : IHasNullableIdentity
{
    public Guid? Id { get; init; }

    [Required, MinLength(1)]
    public string Name { get; init; } = null!;

    public int Order { get; init; }

    public IReadOnlyList<string> Permissions { get; init; } = [];
}
