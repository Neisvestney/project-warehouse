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

    public static class Transfers
    {
        public const string Execute         = "transfers.execute";
        public const string ExecuteAssigned = "transfers.execute_assigned";
    }

    public static class Writeoffs
    {
        public const string View         = "writeoffs.view";
        public const string Edit         = "writeoffs.edit";
        public const string ViewAssigned = "writeoffs.view_assigned";
        public const string EditAssigned = "writeoffs.edit_assigned";
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

    public static class Orders
    {
        public const string View             = "orders.view";
        public const string Edit             = "orders.edit";
        public const string ViewAssigned     = "orders.view_assigned";
        public const string EditAssigned     = "orders.edit_assigned";

        /// <summary>Allows executing assembly tasks for warehouses assigned to the user.</summary>
        public const string AssembleAssigned = "orders.assemble_assigned";

        /// <summary>Allows taking a Confirmed order from an assigned warehouse without admin involvement.</summary>
        public const string SelfAssign       = "orders.self_assign";
    }

    /// <summary>
    /// No _assigned variants: a marketplace account belongs to the shop as a whole, not to a warehouse.
    /// Map is split from Edit so a merchandiser can map cards and run syncs without touching API keys.
    /// Grant Map together with catalog.view and warehouses.view — the mapping pickers read both.
    /// </summary>
    public static class Integrations
    {
        public const string View = "integrations.view";
        public const string Edit = "integrations.edit";
        public const string Map  = "integrations.map";
    }
}
