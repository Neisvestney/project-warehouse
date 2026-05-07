namespace ProjectWarehouse.Server.Models.Users;

public class UserDto
{
    public Guid Id { get; init; }
    public string Username { get; init; } = null!;
    public string? Email { get; init; }
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
}
