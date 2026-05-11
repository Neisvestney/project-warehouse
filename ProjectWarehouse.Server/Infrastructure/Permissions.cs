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
        public const string View          = "users.view";
        public const string Create        = "users.create";
        public const string EditProfile   = "users.edit_profile";
        public const string Delete        = "users.delete";
        public const string ManageRoles   = "users.manage_roles";
        public const string ResetPassword = "users.reset_password";
    }

    public static class Roles
    {
        public const string View              = "roles.view";
        public const string Create            = "roles.create";
        public const string Edit              = "roles.edit";
        public const string Delete            = "roles.delete";
        public const string ManagePermissions = "roles.manage_permissions";
    }
}
