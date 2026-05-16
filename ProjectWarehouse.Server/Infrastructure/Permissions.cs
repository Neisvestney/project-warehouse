using System.Reflection;

namespace ProjectWarehouse.Server.Infrastructure;

public static class Permissions
{
    public static IReadOnlyList<string> All { get; } = typeof(Permissions)
        .GetNestedTypes()
        .SelectMany(t => t.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy))
        .Where(f => f.IsLiteral && !f.IsInitOnly && f.FieldType == typeof(string))
        .Select(f => (string)f.GetRawConstantValue()!)
        .ToList();

    public static class Users
    {
        public const string View                        = "users.view";
        public const string Create                      = "users.create";
        public const string EditProfile                 = "users.edit_profile";
        public const string Delete                      = "users.delete";
        public const string ManageRolesAndPermissions   = "users.manage_roles_and_permissions";
        public const string ResetPassword               = "users.reset_password";
    }

    public static class Roles
    {
        public const string View = "roles.view";
        public const string Edit = "roles.edit";
    }

    public static class Warehouses
    {
        public const string View = "warehouses.view";
        public const string Edit = "warehouses.edit";
    }

    public static class Catalog
    {
        public const string View = "catalog.view";
        public const string Edit = "catalog.edit";
    }

    public static class ChangeLog
    {
        public const string View = "changelog.view";
    }
}
