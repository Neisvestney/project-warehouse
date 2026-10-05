namespace ProjectWarehouse.Server.Infrastructure;

/// <summary>Named action constants used in ChangeLog entries for organizations.</summary>
public static class OrganizationActions
{
    /// <summary>Created by sync from the seller info of an account whose INN matched no organization.</summary>
    public const string AutoCreated = "organization.auto_created";
}
