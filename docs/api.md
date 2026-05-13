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
| PUT | `/api/auth/password` | Bearer | Change own password |
| GET | `/api/auth/me` | Bearer | Current user info + roles + permissions |

---

## Users — `/api/users`

| Method | Path | Permission | Description |
|--------|------|------------|-------------|
| GET | `/api/users` | `users.view` | List all users (paginated) |
| GET | `/api/users/{id}` | `users.view` | Get user by ID |
| POST | `/api/users` | `users.create` | Create user |
| PUT | `/api/users/{id}` | `users.edit` | Update profile, roles, and direct permissions atomically |
| DELETE | `/api/users/{id}` | `users.delete` | Delete user |
| PUT | `/api/users/{id}/password` | `users.reset_password` | Reset another user's password |

---

## Roles — `/api/roles`

| Method | Path | Permission | Description |
|--------|------|------------|-------------|
| GET | `/api/roles` | `roles.view` | List all roles with permissions |
| GET | `/api/roles/search` | `roles.view` | Search roles by name (max 10) |
| PUT | `/api/roles` | `roles.edit` | Atomically replace the entire roles collection |

---

## Warehouses — `/api/warehouses`

| Method | Path | Permission | Description |
|--------|------|------------|-------------|
| GET | `/api/warehouses` | `warehouses.view` | List all warehouses (paginated) |
| GET | `/api/warehouses/{id}` | `warehouses.view` | Get warehouse by ID |
| POST | `/api/warehouses` | `warehouses.edit` | Create warehouse |
| PUT | `/api/warehouses/{id}` | `warehouses.edit` | Update warehouse and sync storage places |
| DELETE | `/api/warehouses/{id}` | `warehouses.edit` | Delete warehouse |

---

## Storage Place Nodes — `/api/storagePlaces/{id}/nodes`

| Method | Path | Permission | Description |
|--------|------|------------|-------------|
| GET | `/api/storagePlaces/{id}/nodes` | `warehouses.view` | Flat list of all nodes |
| POST | `/api/storagePlaces/{id}/nodes` | `warehouses.edit` | Add node, returns updated list |
| PUT | `/api/storagePlaces/{id}/nodes/{nodeId}` | `warehouses.edit` | Update node, returns updated list |
| DELETE | `/api/storagePlaces/{id}/nodes/{nodeId}` | `warehouses.edit` | Delete node (fails if has children), returns updated list |

---

## Permissions — `/api/permissions`

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| GET | `/api/permissions` | Bearer | All defined permission strings |