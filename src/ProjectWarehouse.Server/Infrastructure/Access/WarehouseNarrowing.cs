namespace ProjectWarehouse.Server.Infrastructure.Access;

/// <summary>
/// How far a query must be narrowed for the current user. The three states are kept apart deliberately:
/// a bare <c>HashSet&lt;Guid&gt;?</c> made "sees everything" and "unusable token" both look like <c>null</c>,
/// and every caller had to re-check the permission to tell them apart.
/// </summary>
public readonly record struct WarehouseNarrowing
{
    private WarehouseNarrowing(AccessDenial denial, HashSet<Guid>? ids)
    {
        Denial = denial;
        Ids = ids;
    }

    public AccessDenial Denial { get; }

    /// <summary>The warehouses to filter by, or <c>null</c> when no filtering applies. Meaningless unless allowed.</summary>
    public HashSet<Guid>? Ids { get; }

    /// <summary>Feed to <c>AccessError</c> — turns the token failure into a 401 and everything else into no error.</summary>
    public AccessVerdict Verdict =>
        Denial == AccessDenial.None ? AccessVerdict.Allow : AccessVerdict.TokenInvalid;

    /// <summary>The user holds the unscoped permission: every row is in range.</summary>
    public static WarehouseNarrowing Unrestricted { get; } = new(AccessDenial.None, null);

    public static WarehouseNarrowing TokenInvalid { get; } = new(AccessDenial.TokenInvalid, null);

    public static WarehouseNarrowing RestrictedTo(HashSet<Guid> ids) => new(AccessDenial.None, ids);
}
