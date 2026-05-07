namespace ProjectWarehouse.Server.Models.Auth;

public class MeResponse
{
    public Guid Id { get; init; }
    public string Username { get; init; } = null!;
    public string? Email { get; init; }
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
    public IReadOnlyList<string> Roles { get; init; } = [];
    public IReadOnlyList<string> Permissions { get; init; } = [];
}
