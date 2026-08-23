namespace ProjectWarehouse.Server.Infrastructure;

/// <summary>Named action constants used in ChangeLog entries for marketplace integrations.</summary>
public static class MarketplaceActions
{
    public const string AccountCreated = "account.created";
    public const string AccountUpdated = "account.updated";
    public const string AccountDeleted = "account.deleted";

    /// <summary>Records that the key changed — never the value, old or new.</summary>
    public const string AccountKeyRotated = "account.key_rotated";

    // No sync.started: the changelog only records real state diffs, and a start changes nothing on the
    // account. The running state is visible in MarketplaceSyncRun, which is committed immediately.
    public const string SyncFinished = "sync.finished";

    public const string MappingSet     = "mapping.set";
    public const string MappingCleared = "mapping.cleared";
    public const string MappingAuto    = "mapping.auto";

    public const string RuleCreated = "rule.created";
    public const string RuleUpdated = "rule.updated";
    public const string RuleDeleted = "rule.deleted";
}
