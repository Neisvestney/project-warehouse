namespace ProjectWarehouse.Server.Domain;

public class RolePermission
{
    public Guid RoleId { get; set; }
    public ApplicationRole Role { get; set; } = null!;
    public string Permission { get; set; } = null!;
}
