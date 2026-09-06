namespace ProjectWarehouse.Server.Services;

public interface IPermissionService
{
    /// <summary>Role permissions unioned with direct ones — the set a token carries and <c>/api/auth/me</c> reports.</summary>
    Task<IReadOnlyList<string>> GetEffectivePermissionsAsync(Guid userId);
    Task BumpForRoleUsersAsync(Guid roleId);
    Task BumpUsersAsync(IEnumerable<Guid> userIds);
}
