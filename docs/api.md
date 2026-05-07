# REST API Reference

Base URL: `https://localhost:7095` (dev) / configured host (prod)  
All requests/responses use `application/json`.  
Error format: see [errors.md](errors.md).  
Permission strings: see [permissions.md](permissions.md).

## Authentication

Endpoints marked **Bearer** require `Authorization: Bearer <accessToken>`.  
Endpoints marked with a permission string additionally require that permission in the JWT claims.  
See [auth.md](auth.md) for the full auth flow and token refresh.

---

## Auth — `/api/auth`

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| POST | `/api/auth/login` | — | Get token pair |
| POST | `/api/auth/refresh` | — | Rotate refresh token |
| POST | `/api/auth/logout` | Bearer | Revoke refresh token |
| GET | `/api/auth/me` | Bearer | Current user info + roles + permissions |

### POST `/api/auth/login`

```http
POST /api/auth/login
Content-Type: application/json

{ "username": "admin", "password": "Admin1234!" }
```

**200** `TokenResponse`  
**401** `invalidCredentials`

### POST `/api/auth/refresh`

```http
POST /api/auth/refresh
Content-Type: application/json

{ "refreshToken": "base64string" }
```

**200** `TokenResponse` (new pair; old refresh token is revoked)  
**401** `refreshTokenInvalid`

### POST `/api/auth/logout`

```http
POST /api/auth/logout
Authorization: Bearer eyJ...
Content-Type: application/json

{ "refreshToken": "base64string" }
```

**204** No Content

### GET `/api/auth/me`

**200** `MeResponse`  
**401** `tokenInvalid` (if `sub` claim is missing or unparseable)

---

## Users — `/api/users`

| Method | Path | Permission | Description |
|--------|------|------------|-------------|
| GET | `/api/users` | `users.view` | List all users |
| GET | `/api/users/{id}` | `users.view` | Get user by ID |
| POST | `/api/users` | `users.create` | Create user |
| PUT | `/api/users/{id}` | `users.edit` | Update profile |
| DELETE | `/api/users/{id}` | `users.delete` | Delete user |
| GET | `/api/users/{id}/permissions` | `users.view` | Effective permissions |
| POST | `/api/users/{id}/permissions` | `users.manage_permissions` | Assign direct permission |
| DELETE | `/api/users/{id}/permissions/{permission}` | `users.manage_permissions` | Remove direct permission |
| GET | `/api/users/{id}/roles` | `users.view` | Assigned roles |
| POST | `/api/users/{id}/roles` | `users.manage_roles` | Assign role |
| DELETE | `/api/users/{id}/roles/{roleId}` | `users.manage_roles` | Remove role |

### GET `/api/users`

**200** `UserDto[]`

### GET `/api/users/{id}`

**200** `UserDto`  
**404** `userNotFound`

### POST `/api/users`

Body: `CreateUserRequest`  
**201** `UserDto` (Location header → `/api/users/{id}`)  
**409** `userAlreadyExists` (field: `username`)  
**422** Identity password validation errors (field: `root`)

### PUT `/api/users/{id}`

Body: `UpdateUserRequest`  
**200** `UserDto`  
**404** `userNotFound`

### DELETE `/api/users/{id}`

**204** No Content  
**404** `userNotFound`

### GET `/api/users/{id}/permissions`

**200** `string[]` — effective permissions (role + direct, deduplicated)  
**404** `userNotFound`

### POST `/api/users/{id}/permissions`

Body: `AssignPermissionRequest`  
**204** No Content  
**404** `userNotFound` or `permissionNotFound`  
**409** `permissionAlreadyAssigned`

### DELETE `/api/users/{id}/permissions/{permission}`

**204** No Content  
**404** `userNotFound` or `permissionNotFound`

### GET `/api/users/{id}/roles`

**200** `string[]` — role names  
**404** `userNotFound`

### POST `/api/users/{id}/roles`

Body: `AssignRoleRequest`  
**204** No Content  
**404** `userNotFound` or `roleNotFound`

### DELETE `/api/users/{id}/roles/{roleId}`

**204** No Content  
**404** `userNotFound` or `roleNotFound`

---

## Roles — `/api/roles`

| Method | Path | Permission | Description |
|--------|------|------------|-------------|
| GET | `/api/roles` | `roles.view` | List all roles |
| GET | `/api/roles/{id}` | `roles.view` | Get role by ID |
| POST | `/api/roles` | `roles.create` | Create role |
| PUT | `/api/roles/{id}` | `roles.edit` | Rename role |
| DELETE | `/api/roles/{id}` | `roles.delete` | Delete role |
| GET | `/api/roles/{id}/permissions` | `roles.view` | Role's permissions |
| POST | `/api/roles/{id}/permissions` | `roles.manage_permissions` | Assign permission to role |
| DELETE | `/api/roles/{id}/permissions/{permission}` | `roles.manage_permissions` | Remove permission from role |

### GET `/api/roles`

**200** `RoleDto[]`

### GET `/api/roles/{id}`

**200** `RoleDto`  
**404** `roleNotFound`

### POST `/api/roles`

Body: `CreateRoleRequest`  
**201** `RoleDto`  
**409** `roleAlreadyExists` (field: `name`)

### PUT `/api/roles/{id}`

Body: `UpdateRoleRequest`  
**200** `RoleDto`  
**403** `roleProtected` (Admin role)  
**404** `roleNotFound`

### DELETE `/api/roles/{id}`

**204** No Content  
**403** `roleProtected` (Admin role)  
**404** `roleNotFound`

### GET `/api/roles/{id}/permissions`

**200** `string[]`  
**404** `roleNotFound`

### POST `/api/roles/{id}/permissions`

Body: `AssignRolePermissionRequest`  
**204** No Content  
**404** `roleNotFound` or `permissionNotFound`  
**409** `permissionAlreadyAssigned`

### DELETE `/api/roles/{id}/permissions/{permission}`

**204** No Content  
**404** `roleNotFound` or `permissionNotFound`

---

## Permissions — `/api/permissions`

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| GET | `/api/permissions` | Bearer | All defined permission strings |

### GET `/api/permissions`

**200** `string[]` — all values from `Permissions.All`

---

## Models

### `TokenResponse`
```json
{
  "accessToken": "eyJ...",
  "refreshToken": "base64string",
  "expiresIn": 900
}
```
`expiresIn` is seconds until access token expires (default 900 = 15 min).

### `MeResponse`
```json
{
  "id": "uuid",
  "username": "admin",
  "email": "admin@example.com",
  "firstName": "Admin",
  "lastName": null,
  "roles": ["Admin"],
  "permissions": ["users.view", "users.create", "..."]
}
```
`email`, `firstName`, `lastName` are nullable.

### `UserDto`
```json
{
  "id": "uuid",
  "username": "john",
  "email": "john@example.com",
  "firstName": "John",
  "lastName": "Doe"
}
```

### `RoleDto`
```json
{ "id": "uuid", "name": "Operator" }
```

### `LoginRequest`
```json
{ "username": "admin", "password": "Admin1234!" }
```
Both fields required.

### `RefreshRequest`
```json
{ "refreshToken": "base64string" }
```

### `CreateUserRequest`
```json
{
  "username": "john",
  "password": "SecurePass1!",
  "email": "john@example.com",
  "firstName": "John",
  "lastName": "Doe"
}
```
`username` and `password` required. Password validated by ASP.NET Core Identity (min 8 chars by default).

### `UpdateUserRequest`
```json
{ "email": "new@example.com", "firstName": "Jane", "lastName": null }
```
All fields optional (nulls are applied).

### `AssignRoleRequest`
```json
{ "roleId": "uuid" }
```

### `AssignPermissionRequest` / `AssignRolePermissionRequest`
```json
{ "permission": "users.view" }
```
`permission` must be a value from `GET /api/permissions`. Invalid string → `permissionNotFound`.

### `CreateRoleRequest` / `UpdateRoleRequest`
```json
{ "name": "Operator" }
```
`name` is required in both.
