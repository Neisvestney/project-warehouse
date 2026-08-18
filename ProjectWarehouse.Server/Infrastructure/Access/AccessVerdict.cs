namespace ProjectWarehouse.Server.Infrastructure.Access;

public enum AccessDenial
{
    None,
    NoPermission,
    NotAssigned,
    TokenInvalid,
}

/// <summary>
/// Why access was refused. Controllers need the reason to keep their per-entity error codes;
/// <see cref="Services.IEntityAccessService"/> collapses it to a bool.
/// </summary>
public readonly record struct AccessVerdict(AccessDenial Denial, ErrorCode Code, string Message)
{
    public bool Allowed => Denial == AccessDenial.None;

    public static AccessVerdict Allow { get; } = new(AccessDenial.None, ErrorCode.PermissionDenied, "");

    public static AccessVerdict NoPermission { get; } = new(AccessDenial.NoPermission,
        ErrorCode.PermissionDenied, "You do not have permission to perform this action.");

    public static AccessVerdict TokenInvalid { get; } = new(AccessDenial.TokenInvalid,
        ErrorCode.TokenInvalid, "Invalid token.");

    public static AccessVerdict NotAssigned(ErrorCode code, string message) =>
        new(AccessDenial.NotAssigned, code, message);
}
