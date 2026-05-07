namespace ProjectWarehouse.Server.Domain;

public class UserPermission
{
    public Guid UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;
    public string Permission { get; set; } = null!;
}
