namespace ProjectWarehouse.Server.Models.Users;

public class UpdateUserRequest
{
    public string? Email { get; init; }
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
}
