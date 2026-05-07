# Permissions System

## Design

Permissions are **static string constants** defined in `Infrastructure/Permissions.cs`. They are grouped into nested static classes by module. `Permissions.All` (auto-populated via reflection) contains every defined permission — used for seeding and policy registration.

## Adding a New Permission

1. Open `ProjectWarehouse.Server/Infrastructure/Permissions.cs`
2. Add a `const string` to an existing module class (or create a new nested class):

```csharp
public static class Items
{
    public const string View   = "items.view";
    public const string Create = "items.create";
    public const string Edit   = "items.edit";
    public const string Delete = "items.delete";
}
```

3. Add a policy-based `[Authorize]` attribute to your controller action:

```csharp
[HttpGet]
[Authorize(Policy = Permissions.Items.View)]
public async Task<IActionResult> GetAll() { ... }
```

That's it. `Permissions.All` picks up new constants automatically via reflection. The authorization policy is registered at startup for every entry in `Permissions.All`.

## Available Permissions

### Users (`users.*`)
| Permission | Constant |
|-----------|----------|
| `users.view` | `Permissions.Users.View` |
| `users.create` | `Permissions.Users.Create` |
| `users.edit` | `Permissions.Users.Edit` |
| `users.delete` | `Permissions.Users.Delete` |
| `users.manage_roles` | `Permissions.Users.ManageRoles` |
| `users.manage_permissions` | `Permissions.Users.ManagePermissions` |

### Roles (`roles.*`)
| Permission | Constant |
|-----------|----------|
| `roles.view` | `Permissions.Roles.View` |
| `roles.create` | `Permissions.Roles.Create` |
| `roles.edit` | `Permissions.Roles.Edit` |
| `roles.delete` | `Permissions.Roles.Delete` |
| `roles.manage_permissions` | `Permissions.Roles.ManagePermissions` |

## RBAC + Direct Permissions

A user's **effective permissions** = union of:
1. All permissions assigned to any of the user's roles (`RolePermission` table)
2. Any permissions assigned directly to the user (`UserPermission` table)

These are embedded in the JWT as individual `permission` claims. The `PermissionAuthorizationHandler` checks `User.HasClaim("permission", requiredPermission)` — pure in-memory, no DB access per request.

## Managing via API

See [Auth docs](auth.md) for getting a token first.

**Assign permission to role:**
```http
POST /api/roles/{roleId}/permissions
Authorization: Bearer ...
Content-Type: application/json

{ "permission": "items.view" }
```

**Assign direct permission to user:**
```http
POST /api/users/{userId}/permissions
Authorization: Bearer ...
Content-Type: application/json

{ "permission": "items.delete" }
```

**Get all valid permission strings:**
```http
GET /api/permissions
Authorization: Bearer ...
```

## Admin Role Protection

The `Admin` role:
- Is seeded at startup with **all permissions** from `Permissions.All`
- Cannot be deleted via `DELETE /api/roles/{id}` (returns 403)
- Cannot be renamed via `PUT /api/roles/{id}` (returns 403)

When new permissions are added to the code, they are **not** automatically added to the Admin role after the first seed. Re-seed manually or add a migration step if needed.

## Token Invalidation on Permission Change

When role or user permissions change, `SecurityVersionStore.BumpAsync(userId)` is called for all affected users. Their existing JWTs become invalid — the client must refresh. See [auth.md](auth.md) for the full TOKEN_OUTDATED flow.
