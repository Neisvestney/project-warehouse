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
        "detail": "userAlreadyExists: Username is already taken",
        "args": null
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
        "detail": "invalidCredentials: Username or password is incorrect.",
        "args": null
      }
    ]
  }
}
```

Error with structured arguments (e.g. password too short):
```json
{
  "status": 422,
  "title": "Unprocessable Entity",
  "errors": {
    "root": [
      {
        "code": "passwordTooShort",
        "detail": "passwordTooShort: Passwords must be at least 8 characters.",
        "args": { "minimalLength": 8 }
      }
    ]
  }
}
```

`code` is a camelCase string matching the `ErrorCode` enum. `detail` is `"code: human-readable message"` and is
developer-facing English — clients never render it. `args` is an optional object with extra context for message
formatting, `null` when none is needed.

## Where the codes are documented

There is no code list here. Each endpoint's XML `<remarks>` names the codes it can raise and the condition for
each; `ErrorCode` in `Infrastructure/ErrorCode.cs` is the complete enum. On the client, `errorCodeArgMessages` in
`src/utils/errorUtils.ts` holds the Russian message templates and is the practical index of which codes carry
`args` and what those args are called.

An `args` shape belongs to its code, not to the endpoint: the same code raised from three places carries the same
keys. Adding a key is a breaking change for every consumer formatting that message.

## Errors that get persisted

Some errors outlive their response. `MarketplaceSyncRun.Error` and `MarketplaceAccount.LastSyncError` store a
whole `AppFieldError` in a `jsonb` column, so a failed run keeps its machine-readable `code` and `args` instead of
a prose string — which is what lets the UI render a real message for a sync that failed overnight.

In that column `ErrorCode` is serialized as its **integer** value (Npgsql's serializer, not the MVC one). That is
why the enum's numbers are pinned explicitly: a new code takes the next free number, and an existing member is
never renumbered. See [backend-patterns.md](backend-patterns.md#enums-pinned-values-free-ordering).

Request headers are never included in a persisted error — that is where the API key travels.

## Field Path Conventions

The `errors` key maps a **field path** to an array of errors. Paths point to the exact location in the request body that caused the problem.

### Simple field
A top-level property of the request body. Use the camelCase property name.

```
POST /api/users  →  "username", "password", "email"
```

### Nested property
A property inside a nested object. Use dot notation.

```
PUT /api/users/{id}  →  "address.city", "address.zip"
```

### Array element property
A property inside an object that is an element of an array. Use `[index].property`.

```
PUT /api/roles  →  "[2].permissions[0]", "[0].name"
```

| Situation | Field path |
|-----------|-----------|
| Unknown permission at index 0 in item 2 | `[2].permissions[0]` |
| Invalid role ID in item 5 | `[5].id` |
| Missing name in item 1 | `[1].name` |

### Root (non-field) errors
Errors that don't belong to a specific field — auth failures, entity-not-found, unexpected server errors. Always use `"root"`.

```
Unauthorized, Forbidden, role not found, unhandled exception  →  "root"
```

---

## Controller Helpers

All controllers extend `AppControllerBase`, which provides one-liner error returns:

```csharp
// Root errors
return Unauthorized(ErrorCode.InvalidCredentials, "Username or password is incorrect.");
return Forbidden();  // defaults to permissionDenied
return Forbidden(ErrorCode.RoleProtected, "The Admin role cannot be deleted.", new Dictionary<string, object> { ["roleName"] = "Admin" });
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
    ("username", ErrorCode.Required, "Username is required.", null),
    ("password", ErrorCode.TooShort,  "Password must be at least 8 characters.", null),
}));
```
