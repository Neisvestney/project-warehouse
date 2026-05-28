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
        public const string ManageAssignedWarehouses    = "users.manage_assigned_warehouses";
        public const string ResetPassword               = "users.reset_password";
    }

    public static class Roles
    {
        public const string View = "roles.view";
        public const string Edit = "roles.edit";
    }

    public static class Warehouses
    {
        public const string View         = "warehouses.view";
        public const string Edit         = "warehouses.edit";
        public const string ViewAssigned = "warehouses.view_assigned";
        public const string EditAssigned = "warehouses.edit_assigned";
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

    public static class Receipts
    {
        public const string View            = "receipts.view";
        public const string Edit            = "receipts.edit";
        public const string ViewAssigned    = "receipts.view_assigned";
        public const string EditAssigned    = "receipts.edit_assigned";

        /// <summary>
        /// Allows processing (receiving + placement) for warehouses assigned to the user.
        /// Checked alongside warehouse assignment validation.
        /// </summary>
        public const string ProcessAssigned = "receipts.process_assigned";
    }
}
