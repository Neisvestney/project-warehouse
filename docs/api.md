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
| GET | `/api/warehouses/{id}/print` | `warehouses.view` | All nodes as `StoragePlaceNodePrintDto[]` ordered by full path (for label printing) |
| GET | `/api/warehouses/{id}/items-groups` | `warehouses.view` | List all item groups in a warehouse (`Paginated<ItemsGroupDto>`), supports `searchString` |
| POST | `/api/warehouses` | `warehouses.edit` | Create warehouse |
| PUT | `/api/warehouses/{id}` | `warehouses.edit` | Update warehouse and sync storage places |
| DELETE | `/api/warehouses/{id}` | `warehouses.edit` | Delete warehouse |

`StoragePlaceNodePrintDto` shape: `{ id: Guid, name: string[] }` — `name` is the full breadcrumb path from storage place root down to the node (e.g. `["Стеллаж А", "Полка 1", "Ячейка 3"]`).

---

## Storage Place Nodes — `/api/storagePlaces/{id}/nodes`

| Method | Path | Permission | Description |
|--------|------|------------|-------------|
| GET | `/api/storagePlaces/{id}/nodes` | `warehouses.view` | Flat list of all nodes (`StoragePlaceNodeDto[]` ordered by name) |
| POST | `/api/storagePlaces/{id}/nodes` | `warehouses.edit` | Add node, returns updated flat list |
| PUT | `/api/storagePlaces/{id}/nodes/{nodeId}` | `warehouses.edit` | Update node name/parent, returns updated flat list |
| DELETE | `/api/storagePlaces/{id}/nodes/{nodeId}` | `warehouses.edit` | Delete node (fails with `storagePlaceNodeHasChildren` if it has children), returns updated flat list |
| GET | `/api/storagePlaces/{id}/nodes/{nodeId}` | `warehouses.view` | Node details including item groups (`StoragePlaceNodeDetailsDto`) |
| PUT | `/api/storagePlaces/{id}/nodes/{nodeId}/items` | `warehouses.edit` | Atomically sync item groups for a node, returns updated `StoragePlaceNodeDetailsDto` |

**Item group sync rules** (`PUT .../items` body: `NodeItemsGroupItem[]`):
- `id: null` → create new item group
- `id` present → update existing item group
- existing group not in the list → delete

Returns 422 `storagePlaceNodeItemsGroupNotFound` if any provided ID does not belong to this node.  
Returns 422 `catalogItemCharacteristicNotFound` if any `catalogItemWithCharacteristicId` does not exist.  
Returns 422 `catalogItemCharacteristicDuplicate` if the same `catalogItemWithCharacteristicId` appears more than once.

---

## Catalog — `/api/catalog`

| Method | Path | Permission | Description |
|--------|------|------------|-------------|
| GET | `/api/catalog` | `catalog.view` | List catalog items paginated (`Paginated<CatalogItemSummaryDto>`), supports `searchString` |
| GET | `/api/catalog/{id}` | `catalog.view` | Get catalog item with characteristics (`CatalogItemDto`) |
| POST | `/api/catalog` | `catalog.edit` | Create catalog item with optional characteristics |
| PUT | `/api/catalog/{id}` | `catalog.edit` | Update catalog item and atomically sync characteristics |
| DELETE | `/api/catalog/{id}` | `catalog.edit` | Delete catalog item and all its characteristics |

**Characteristic sync rules** (`PUT /api/catalog/{id}` body: `UpdateCatalogItemRequest`):
- `id: null` → create new characteristic
- `id` present → update existing characteristic
- existing characteristic not in the list → delete

Returns 422 `catalogItemCharacteristicNotFound` if any provided characteristic ID does not belong to this item.

**Key DTOs:**

`CatalogItemSummaryDto`: `{ id, name, article, barcode?, characteristicCount }`  
`CatalogItemDto`: `{ id, name, article, barcode?, characteristics: CatalogItemCharacteristicDto[] }`  
`CatalogItemCharacteristicDto`: `{ id, characteristic, barcode? }`

---

## Permissions — `/api/permissions`

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| GET | `/api/permissions` | Bearer | All defined permission strings |