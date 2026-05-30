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
| GET | `/api/users/{id}` | `users.view` or self | Get user by ID; always allowed if `id` == current user |
| POST | `/api/users` | `users.create` | Create user |
| PUT | `/api/users/{id}` | `users.edit_profile` | Update profile, roles, and direct permissions atomically |
| DELETE | `/api/users/{id}` | `users.delete` | Delete user |
| PUT | `/api/users/{id}/password` | `users.reset_password` | Reset another user's password |

---

## Roles — `/api/roles`

| Method | Path | Permission | Description |
|--------|------|------------|-------------|
| GET | `/api/roles` | `roles.view` | List all roles with permissions |
| GET | `/api/roles/{id}` | `roles.view` | Get role by ID |
| GET | `/api/roles/search` | `roles.view` | Search roles by name (max 10) |
| PUT | `/api/roles` | `roles.edit` | Atomically replace the entire roles collection |

---

## Warehouses — `/api/warehouses`

| Method | Path | Permission | Description |
|--------|------|------------|-------------|
| GET | `/api/warehouses` | `warehouses.view` or `warehouses.view_assigned` | List warehouses; `view_assigned` returns only user's assigned warehouses |
| GET | `/api/warehouses/{id}` | `warehouses.view` or `warehouses.view_assigned` | Get warehouse by ID; `view_assigned` returns 403 if warehouse not assigned |
| GET | `/api/warehouses/{id}/print` | `warehouses.view` or `warehouses.view_assigned` | All nodes as `StoragePlaceNodePrintDto[]` ordered by full path (for label printing) |
| GET | `/api/warehouses/{id}/items-groups` | `warehouses.view` or `warehouses.view_assigned` | List all item groups in a warehouse (`Paginated<ItemsGroupDto>`), supports `searchString` |
| POST | `/api/warehouses` | `warehouses.edit` | Create warehouse |
| PUT | `/api/warehouses/{id}` | `warehouses.edit` or `warehouses.edit_assigned` | Update warehouse and sync storage places; `edit_assigned` returns 403 if warehouse not assigned |
| DELETE | `/api/warehouses/{id}` | `warehouses.edit` or `warehouses.edit_assigned` | Delete warehouse; `edit_assigned` returns 403 if warehouse not assigned |

`StoragePlaceNodePrintDto` shape: `{ id: Guid, name: string[] }` — `name` is the full breadcrumb path from storage place root down to the node (e.g. `["Стеллаж А", "Полка 1", "Ячейка 3"]`).

---

## Storage Place Nodes — `/api/storagePlaces/{id}/nodes`

| Method | Path | Permission | Description |
|--------|------|------------|-------------|
| GET | `/api/storagePlaces/{id}/nodes` | `warehouses.view` | Flat list of all nodes (`StoragePlaceNodeDto[]` ordered by `order` then `name`) |
| POST | `/api/storagePlaces/{id}/nodes` | `warehouses.edit` | Add node, returns updated flat list |
| PUT | `/api/storagePlaces/{id}/nodes/reorder` | `warehouses.edit` | Bulk-update `order` for a set of nodes (`NodeOrderItem[]`), returns updated flat list |
| PUT | `/api/storagePlaces/{id}/nodes/{nodeId}` | `warehouses.edit` | Update node name/parent/order, returns updated flat list |
| DELETE | `/api/storagePlaces/{id}/nodes/{nodeId}` | `warehouses.edit` | Delete node (fails with `storagePlaceNodeHasChildren` if it has children), returns updated flat list |
| GET | `/api/storagePlaces/{id}/nodes/{nodeId}` | `warehouses.view` | Node details including item groups (`StoragePlaceNodeDetailsDto`) |
| PUT | `/api/storagePlaces/{id}/nodes/{nodeId}/items` | `warehouses.edit` | Atomically sync item groups for a node, returns updated `StoragePlaceNodeDetailsDto` |

**Item group sync rules** (`PUT .../items` body: `NodeItemsGroupItem[]`):
- `id: null` → create new item group
- `id` present → update existing item group
- existing group not in the list → delete

**Reorder rules** (`PUT .../reorder` body: `NodeOrderItem[]` — `{ nodeId, order }`):
- Only nodes included in the list are updated; others are unchanged.
- Returns 422 `storagePlaceNodeNotFound` (field: `[i].nodeId`) if any node does not belong to this storage place.

Returns 422 `storagePlaceNodeItemsGroupNotFound` if any provided ID does not belong to this node.  
Returns 422 `catalogItemCharacteristicNotFound` if any `catalogItemWithCharacteristicId` does not exist.  
Returns 422 `catalogItemCharacteristicDuplicate` if the same `catalogItemWithCharacteristicId` appears more than once.

---

## Receipts — `/api/receipts`

Access: `receipts.view` / `receipts.view_assigned` (read), `receipts.edit` / `receipts.edit_assigned` (write), `receipts.process_assigned` (placement ops). `*_assigned` variants are restricted to warehouses assigned to the current user.

### CRUD

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| GET | `/api/receipts` | Bearer | List receipts paginated. Supports `searchString`, `warehouseId`, `status`, `reason`, `sortBy`, `sortOrder`. Access level determined from user permissions. |
| GET | `/api/receipts/{id}` | Bearer | Get full receipt details including items and placements. |
| POST | `/api/receipts` | `receipts.edit` or `receipts.edit_assigned` | Create receipt (always Draft). |
| PATCH | `/api/receipts/{id}` | `receipts.edit` or `receipts.edit_assigned` | Update name/reason/notes. Draft status only. |
| DELETE | `/api/receipts/{id}` | `receipts.edit` | Delete receipt. Draft status only. |

### Items

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| PUT | `/api/receipts/{id}/items` | `receipts.edit` or `receipts.edit_assigned` | Atomically sync the expected items list (`ReceiptItemRequest[]`). Draft or Planned status only. Deduplicates by `catalogItemId`. |
| PATCH | `/api/receipts/{id}/items/{itemId}/received-count` | `receipts.edit` or `receipts.process_assigned` | Update actually received count for one item. Processing status only. |

### Placements

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| POST | `/api/receipts/{id}/items/{itemId}/placements/standard` | `receipts.edit` or `receipts.process_assigned` | Place count-based (Standard) items at a storage node. Processing status only. |
| POST | `/api/receipts/{id}/items/{itemId}/placements/unit` | `receipts.edit` or `receipts.process_assigned` | Place a serialised Unit item (by `inventoryNumber`) at a storage node. Processing status only. |
| POST | `/api/receipts/{id}/items/{itemId}/placements/assembled-bundle` | `receipts.edit` or `receipts.process_assigned` | Place an AssembledBundle at a storage node (components must exactly match the catalog definition). Processing status only. |
| DELETE | `/api/receipts/{id}/items/{itemId}/placements/{placementId}` | `receipts.edit` or `receipts.process_assigned` | Remove a placement, reversing the inventory change. Processing status only. |

### Status transitions

Statuses: `Draft` → `Planned` → `Processing` → `Finished` / `Canceled`

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| POST | `/api/receipts/{id}/plan` | `receipts.edit` or `receipts.edit_assigned` | Draft → Planned |
| POST | `/api/receipts/{id}/start-processing` | `receipts.edit` or `receipts.edit_assigned` | Planned → Processing |
| POST | `/api/receipts/{id}/finish` | `receipts.edit` or `receipts.edit_assigned` | Processing → Finished. Validates all items with `receivedCount` are fully placed (placed == receivedCount). |
| POST | `/api/receipts/{id}/revert` | `receipts.edit` or `receipts.edit_assigned` | Go one status back: Finished→Processing, Processing→Planned (only if no placements), Planned→Draft. |
| POST | `/api/receipts/{id}/cancel` | `receipts.edit` or `receipts.edit_assigned` | Cancel from Draft/Planned/Processing (Processing only if no placements). |

**Items sync** (`PUT .../items` body: `ReceiptItemRequest[]`):

| Field | Type | Description |
|-------|------|-------------|
| `catalogItemId` | `Guid` | Reference to an existing `CatalogItem` |
| `plannedCount` | `int` | Expected quantity (≥ 1) |
| `notes` | `string?` | Optional item note |

Existing items not in the list are removed. Duplicate `catalogItemId` values in the same request → 422 `validationError`.

**Key DTOs:**

`ReceiptSummaryDto`: `{ id, number, name?, reason, status, plannedDeliveryDate?, createdAt, warehouseId, warehouseName, totalPlannedCount, totalReceivedCount }`  
`ReceiptDto`: `{ id, number, name?, reason, status, notes?, plannedDeliveryDate?, createdAt, warehouseId, warehouseName, totalPlannedCount, totalReceivedCount, items: ReceiptItemDto[] }`  
`ReceiptItemDto`: `{ id, catalogItemId, catalogItem: CatalogItemSummaryDto, plannedCount, receivedCount?, notes?, placements: ReceiptItemPlacementDto[] }`  
`ReceiptItemPlacementDto`: `{ id, storagePlaceNodeId, storagePlaceName, storagePlacePath, count, unitInventoryItem?: ..., assembledBundleInventoryItem?: ... }`

**`ReceiptReason` values:** `newGoods`, `return`, `other`  
**`ReceiptStatus` values:** `draft`, `planned`, `processing`, `finished`, `canceled`  
**`ReceiptSortBy` values:** `number` (default), `status`, `createdAt`, `warehouseName`, `name`, `plannedDeliveryDate`

---

## Catalog — `/api/catalog`

| Method | Path | Permission | Description |
|--------|------|------------|-------------|
| GET | `/api/catalog` | `catalog.view` | List catalog items paginated (`Paginated<CatalogItemSummaryDto>`), supports `searchString`, `sortBy` (`name`\|`article`\|`barcode`\|`type`, default `name`), `sortOrder` (`asc`\|`desc`, default `asc`); archived items always sorted last |
| GET | `/api/catalog/{id}` | `catalog.view` | Get full catalog item details (`CatalogItemDto`) |
| GET | `/api/catalog/tags` | `catalog.view` | List tags (ordered by name), supports `search` query param |
| POST | `/api/catalog` | `catalog.edit` | Create catalog item |
| PUT | `/api/catalog/{id}` | `catalog.edit` | Update catalog item and atomically sync type-specific collections (children/components/variationIds/memberIds) |
| DELETE | `/api/catalog/{id}` | `catalog.edit` | Delete catalog item |

**Children sync rules** (ProductGroup only, `PUT /api/catalog/{id}` body: `UpdateCatalogItemRequest`):
- `id: null` → create new child item
- `id` present → update existing child
- existing child not in the list → delete

**Duplicate validation** (both `POST` and `PUT`):
- 422 `catalogItemArticleDuplicate` — field `article`
- 422 `catalogItemBarcodeDuplicate` — field `barcode`
- 422 `catalogItemComponentInvalid` — a component item is of an invalid type for bundles
- 422 `catalogItemVariationInvalid` — a variation ID is invalid or wrong type
- 422 `catalogItemGroupInvalid` — `groupId` does not refer to a ProductGroup
- 422 `catalogItemIsImmutable` — assembledBundle cannot be edited
- 422 `catalogItemManagedByGroup` — item with `groupId` cannot be edited directly

**`CatalogItemType` values:** `standard`, `unit`, `productGroup`, `variation`, `bundle`, `assembledBundle`

**Key DTOs:**

`CatalogItemSummaryDto`: `{ id, type, name, fullName, article, barcode?, isArchived }`  
`CatalogItemDto`: `{ id, type, name, fullName, article, barcode?, description?, notes?, isArchived, groupId?, groupName?, sourceBundleId?, tags: CatalogItemTagDto[], components: BundleComponentDto[], variationIds: Guid[], memberIds: Guid[], children: CatalogItemDto[] }`  
`CatalogItemTagDto`: `{ id, name }`  
`BundleComponentDto`: `{ id, componentId, componentName, quantity }`

---

## Inventory Items — `/api/inventory-items`

| Method | Path | Permission | Description |
|--------|------|------------|-------------|
| GET | `/api/inventory-items` | `warehouses.view` or `warehouses.view_assigned` | Paginated stock overview aggregated by catalog item |
| GET | `/api/inventory-items/units` | `warehouses.view` or `warehouses.view_assigned` | Paginated list of individual unit inventory item instances |
| GET | `/api/inventory-items/assembled-bundles` | `warehouses.view` or `warehouses.view_assigned` | Paginated list of individual assembled bundle instances |

`view_assigned` limits results to warehouses assigned to the current user.

### `GET /api/inventory-items` — GetAll
Query params: `page`, `pageSize`, `searchString?`, `warehouseId?`, `storagePlaceId?`, `nodeId?`, `catalogItemType?` (CatalogItemType), `isArchived?` (bool)  
Returns: `Paginated<InventoryItemSummaryDto>`

Items from all three storage mechanisms (StoragePlaceNodeItemsGroup for standard items, UnitInventoryItem, AssembledBundleInventoryItem) are counted separately per `CatalogItemId` and merged. The result is one row per catalog item with `Count` = total across all locations within the applied filters.

**`InventoryItemSummaryDto`**: `{ catalogItemId: Guid, catalogItem: CatalogItemSummaryDto, count: int }`

### `GET /api/inventory-items/units` — GetAllUnits
Query params: `page`, `pageSize`, `searchString?` (searches SKU), `warehouseId?`, `storagePlaceId?`, `nodeId?`, `catalogItemId?`  
Returns: `Paginated<UnitInventoryItemDto>`

The `catalogItemId` filter is used by the frontend drawer to list all instances of a clicked catalog item.

**`UnitInventoryItemDto`**: `{ id: Guid, sku: string, catalogItem: CatalogItemSummaryDto, warehouseId: Guid, warehouseName: string, storagePlaceId: Guid, storagePlaceName: string, nodeId: Guid, nodeName: string }`

### `GET /api/inventory-items/assembled-bundles` — GetAllAssembledBundles
Query params: `page`, `pageSize`, `searchString?` (searches catalog item name), `warehouseId?`, `storagePlaceId?`, `nodeId?`, `catalogItemId?`  
Returns: `Paginated<AssembledBundleInventoryItemDto>`

**`AssembledBundleInventoryItemDto`**: `{ id: Guid, catalogItem: CatalogItemSummaryDto, warehouseId: Guid, warehouseName: string, storagePlaceId: Guid, storagePlaceName: string, nodeId: Guid, nodeName: string }`

---

## Permissions — `/api/permissions`

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| GET | `/api/permissions` | Bearer | All defined permission strings |