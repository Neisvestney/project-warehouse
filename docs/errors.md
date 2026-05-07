# Error Response Format

## Structure

All API errors return `AppProblemDetails` — a superset of RFC 7807 `ProblemDetails`. Every error is bound to a field name. General errors use the pseudo-field `"root"`.

```json
{
  "status": 422,
  "title": "Unprocessable Entity",
  "errors": {
    "username": [
      {
        "code": "userAlreadyExists",
        "detail": "userAlreadyExists: Username is already taken"
      }
    ]
  }
}
```

General (non-field) error:
```json
{
  "status": 401,
  "title": "Unauthorized",
  "errors": {
    "root": [
      {
        "code": "invalidCredentials",
        "detail": "invalidCredentials: Username or password is incorrect."
      }
    ]
  }
}
```

`code` is a camelCase string matching the `ErrorCode` enum. `detail` is `"code: human-readable message"`.

## ErrorCode Reference

### Auth
| Code | When |
|------|------|
| `invalidCredentials` | Wrong username or password |
| `tokenOutdated` | JWT `security_version` mismatch — call refresh |
| `tokenInvalid` | JWT cannot be validated (malformed, wrong signature) |
| `refreshTokenInvalid` | Refresh token not found in DB |
| `refreshTokenExpired` | Refresh token has passed its `ExpiresAt` |
| `refreshTokenRevoked` | Refresh token was already used or revoked |

### Access
| Code | When |
|------|------|
| `permissionDenied` | User lacks the required permission |
| `roleProtected` | Attempt to delete or modify the Admin role |

### Entities
| Code | When |
|------|------|
| `userNotFound` | User ID not found |
| `roleNotFound` | Role ID not found |
| `permissionNotFound` | Permission string not in `Permissions.All` |
| `userAlreadyExists` | Username already taken |
| `roleAlreadyExists` | Role name already taken |
| `permissionAlreadyAssigned` | Permission already on the role/user |

### Validation
| Code | When |
|------|------|
| `required` | Field missing or null |
| `tooShort` | Value shorter than minimum |
| `tooLong` | Value longer than maximum |
| `invalidFormat` | Wrong type or format (e.g. string where int expected) |
| `outOfRange` | Numeric value out of allowed range |
| `invalidJson` | Request body is not valid JSON |
| `validationError` | Catch-all for unrecognized validation messages |

## Controller Helpers

All controllers extend `AppControllerBase`, which provides one-liner error returns:

```csharp
// Root errors
return Unauthorized(ErrorCode.InvalidCredentials, "Username or password is incorrect.");
return Forbidden();  // defaults to permissionDenied
return NotFound(ErrorCode.UserNotFound, "User not found.");
return Conflict(ErrorCode.UserAlreadyExists, "Username is already taken.");

// Field errors
return ConflictField("username", ErrorCode.UserAlreadyExists, "Username is already taken.");
return UnprocessableEntity("password", ErrorCode.TooShort, "Password must be at least 8 characters.");
```

For custom HTTP status codes or multi-field errors:
```csharp
return Problem(AppProblems.Root(418, ErrorCode.ValidationError, "I'm a teapot."));
return Problem(AppProblems.UnprocessableEntities(new[] {
    ("username", ErrorCode.Required, "Username is required."),
    ("password", ErrorCode.TooShort,  "Password must be at least 8 characters."),
}));
```
