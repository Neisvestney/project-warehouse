namespace ProjectWarehouse.Server.Infrastructure;

public enum ErrorCode
{
    // Auth
    InvalidCredentials,
    TokenOutdated,
    TokenInvalid,
    RefreshTokenInvalid,
    RefreshTokenExpired,
    RefreshTokenRevoked,

    // Access
    PermissionDenied,
    RoleProtected,

    // Entities
    UserNotFound,
    RoleNotFound,
    PermissionNotFound,
    UserAlreadyExists,
    RoleAlreadyExists,
    PermissionAlreadyAssigned,

    // Validation
    Required,
    TooShort,
    TooLong,
    InvalidFormat,
    OutOfRange,
    InvalidJson,
    PasswordTooShort,
    PasswordAtLeastOneDigit,
    PasswordAtLeastOneUppercase,
    PasswordAtLeastOneLowercase,
    PasswordInvalid,
    ValidationError,
}
