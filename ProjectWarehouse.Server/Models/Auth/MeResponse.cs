using ProjectWarehouse.Server.Infrastructure;

namespace ProjectWarehouse.Server.Models.Auth;

public class MeResponse : IHasIdentity
{
    public Guid Id { get; init; }
    public string Username { get; init; } = null!;
    public string FullName { get; init; } = null!;
    public string? Email { get; init; }
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
    public IReadOnlyList<string> Roles { get; init; } = [];
    public IReadOnlyList<string> Permissions { get; init; } = [];

    /// <summary>Warehouses this user is assigned to — what every <c>_assigned</c> permission is scoped by.</summary>
    public IReadOnlyList<Guid> AssignedWarehouseIds { get; init; } = [];
}
