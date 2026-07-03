namespace ProjectWarehouse.Server.Infrastructure;

/// <summary>Named action constants used in ChangeLog entries for orders.</summary>
public static class OrderActions
{
    public const string Created    = "created";
    public const string Updated    = "updated";
    public const string Deleted    = "deleted";
    public const string Confirmed  = "confirmed";
    public const string SentToAssembly = "sent_to_assembly";
    public const string Assembled  = "assembled";
    public const string Shipped    = "shipped";
    public const string Canceled   = "canceled";
    public const string RolledBack = "rolled_back";
    public const string SelfAssigned = "self_assigned";
}
