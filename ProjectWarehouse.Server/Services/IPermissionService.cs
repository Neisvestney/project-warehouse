namespace ProjectWarehouse.Server.Services;

public interface IPermissionService
{
    Task<IReadOnlyList<string>> GetEffectivePermissionsAsync(Guid userId);
    Task AddRolePermissionAsync(Guid roleId, string permission);
    Task RemoveRolePermissionAsync(Guid roleId, string permission);
    Task AddUserPermissionAsync(Guid userId, string permission);
    Task RemoveUserPermissionAsync(Guid userId, string permission);
    Task BumpForRoleUsersAsync(Guid roleId);
}
