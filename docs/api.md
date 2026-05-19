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

## Inbound Orders — `/api/inbound-orders`

| Method | Path | Permission | Description |
|--------|------|------------|-------------|
| GET | `/api/inbound-orders` | `inbound_orders.view` or `inbound_orders.view_assigned_warehouses` | List orders paginated; `_assigned_warehouses` filters to user's assigned warehouses. Supports `searchString`, `warehouseId` |
| GET | `/api/inbound-orders/{id}` | `inbound_orders.view` or `inbound_orders.view_assigned_warehouses` | Get order details (all fields except item groups) |
| POST | `/api/inbound-orders` | `inbound_orders.edit` or `inbound_orders.edit_assigned_warehouses` | Create order (always Draft); `_assigned_warehouses` restricts to user's warehouses |
| PUT | `/api/inbound-orders/{id}` | `inbound_orders.edit` or `inbound_orders.edit_assigned_warehouses` | Update order metadata and assigned users |
| DELETE | `/api/inbound-orders/{id}` | `inbound_orders.edit` or `inbound_orders.edit_assigned_warehouses` | Delete order (Draft or Finished only; 409 if Processing) |
| GET | `/api/inbound-orders/{id}/draft-items-groups` | `inbound_orders.view` or `inbound_orders.view_assigned_warehouses` | Get all draft item groups with optional catalog links |
| PUT | `/api/inbound-orders/{id}/draft-items-groups` | `inbound_orders.edit` or `inbound_orders.edit_assigned_warehouses` | Atomically sync draft items (sync pattern: null id = create, id = update, missing = delete). Draft status only. |
| GET | `/api/inbound-orders/{id}/items-comparison` | `inbound_orders.view` or `inbound_orders.view_assigned_warehouses` | Declared vs processed comparison with shortage/surplus breakdown |
| POST | `/api/inbound-orders/{id}/change-status-to-processing` | `inbound_orders.edit` or `inbound_orders.edit_assigned_warehouses` | Draft→Processing: validates/auto-creates catalog items, copies draft to declared |
| POST | `/api/inbound-orders/{id}/rollback-status-to-draft` | `inbound_orders.edit` or `inbound_orders.edit_assigned_warehouses` | Processing→Draft: only if no processed items exist; deletes declared items |
| POST | `/api/inbound-orders/{id}/change-status-to-finished` | `inbound_orders.edit` or `inbound_orders.edit_assigned_warehouses` | Processing→Finished |
| POST | `/api/inbound-orders/{id}/rollback-status-to-processing` | `inbound_orders.edit` or `inbound_orders.edit_assigned_warehouses` | Finished→Processing |
| POST | `/api/inbound-orders/{id}/try-auto-assign-catalog-items` | `inbound_orders.edit` or `inbound_orders.edit_assigned_warehouses` | Try to auto-assign `CatalogItemWithCharacteristic` to unlinked draft items by matching barcode → article+characteristic → name+characteristic. Draft status only. |

**Draft items sync** (`PUT .../draft-items-groups` body: `{ draftItemsGroups: DraftItemsGroupItem[] }`):
- `id: null` → create new draft item
- `id` present → update existing draft item
- existing item not in list → delete
- Response and all subsequent reads preserve the order of elements as sent in the request (backed by a server-side `Order` field, not exposed in DTOs)

**`DraftItemsGroupItem` fields:**

| Field | Type | Description |
|-------|------|-------------|
| `id` | `Guid?` | Null to create, present to update |
| `name` | `string` | Item name |
| `article` | `string` | Article / SKU |
| `barcode` | `string?` | Barcode for this specific characteristic |
| `rootBarcode` | `string?` | Barcode for the catalog item itself |
| `characteristic` | `string` | Characteristic name |
| `count` | `int` | Quantity (≥ 1) |
| `catalogItemId` | `Guid?` | Optional reference to an existing `CatalogItem` (without characteristic assigned yet) |
| `catalogItemWithCharacteristicId` | `Guid?` | Fully resolved catalog link |
| `createNew` | `bool` | If `true` and `catalogItemWithCharacteristicId` is null, auto-create on `change-status-to-processing` (see below) |

**`change-status-to-processing` auto-create logic** (per draft item, evaluated when `catalogItemWithCharacteristicId` is null and `createNew` is true):

| Condition | Action |
|-----------|--------|
| `catalogItemId == null && createNew == true` | Create new `CatalogItem` (name, article, rootBarcode) + `CatalogItemWithCharacteristic` (characteristic, barcode), assign both |
| `catalogItemId != null && createNew == true` | Add new `CatalogItemWithCharacteristic` (characteristic, barcode) to the existing `CatalogItem`, assign |

Returns 422 `inboundOrderDraftItemsValidationFailed` (root) with field-level errors if:
- Article/rootBarcode already exist in the catalog (or appear more than once within the request)
- Characteristic already exists on the target `CatalogItem`
- Characteristic barcode already exists globally
- Any item still has no catalog link and `createNew == false`

**`try-auto-assign-catalog-items` body**: `{ draftItemsGroupIds: Guid[] }` — list of draft item IDs to process. Empty array = try all unlinked items. Returns updated `InboundOrderDraftItemsGroupDto[]` for the whole order.

Matching priority per item (stops at first match):
1. `CatalogItemWithCharacteristic.Barcode` == `draft.Barcode`
2. `CatalogItem.Article` (case-insensitive) == `draft.Article` AND `Characteristic` (case-insensitive) == `draft.Characteristic`
3. `CatalogItem.Name` (case-insensitive) == `draft.Name` AND `Characteristic` (case-insensitive) == `draft.Characteristic`

Already-linked items (with `catalogItemWithCharacteristicId` set) are skipped silently.

**Key DTOs:**

`InboundOrderSummaryDto`: `{ id, number, status, title?, plannedStartDateTime, warehouse: WarehouseSummaryDto }`  
`InboundOrderDto`: `{ id, number, status, title?, plannedStartDateTime, notes?, warehouse: WarehouseSummaryDto, assignedUsers: UserDto[] }`  
`InboundOrderDraftItemsGroupDto`: `{ id, name, article, barcode?, rootBarcode?, characteristic, count, catalogItem?: NodeCatalogItemDto, catalogItemWithCharacteristic?: NodeCharacteristicDto, createNew: bool }`  
`InboundOrderItemsComparisonDto`: `{ declaredItems: ComparisonItemDto[], processedItems: ComparisonItemDto[], shortages: ItemDifferenceDto[], surpluses: ItemDifferenceDto[], totalShortageCount, totalSurplusCount }`

---

## Inbound Order Processing — `/api/inbound-order-processing`

Requires `inbound_orders.process` permission. All endpoints additionally check that the current user is in the order's `AssignedUsers`.

| Method | Path | Permission | Description |
|--------|------|------------|-------------|
| GET | `/api/inbound-order-processing` | `inbound_orders.process` | List orders assigned to current user (paginated, searchable) |
| GET | `/api/inbound-order-processing/{id}` | `inbound_orders.process` | Order detail with full warehouse schema and storage place order-status flags (Processing only) |
| GET | `/api/inbound-order-processing/{id}/nodes?storagePlaceId={storagePlaceId}` | `inbound_orders.process` | Flat list of nodes for a storage place (required param) |
| GET | `/api/inbound-order-processing/{id}/nodes/{nodeId}` | `inbound_orders.process` | Node details with items groups (includes `storagePlaceId`) |
| POST | `/api/inbound-order-processing/{id}/nodes/{nodeId}/items` | `inbound_orders.process` | Place items in a node for this order (409 if already placed) |
| PUT | `/api/inbound-order-processing/{id}/nodes/{nodeId}/items` | `inbound_orders.process` | Update items in node for this order (delta-based; 422 if trying to remove more than placed) |

**PlaceItems / UpdateItems body**: `{ items: [{ catalogItemWithCharacteristicId: Guid, count: int }] }`  
`PlaceItems` creates new `InboundOrderProcessedItemsGroup` entries AND adds to `StoragePlaceNodeItemsGroup` (physical inventory).  
`UpdateItems` computes delta vs current order-tracked quantities; removes from physical inventory on reduction.

**Key DTOs:**

`InboundOrderProcessingDto`: `{ id, number, status, title?, plannedStartDateTime, notes?, warehouse: ProcessingWarehouseDto }`  
`ProcessingWarehouseDto`: `{ id, name, width, height, storagePlaces: ProcessingStoragePlaceDto[], layoutObjects: WarehouseLayoutElementDto[] }`  
`ProcessingStoragePlaceDto`: `{ id, name, x, y, width, height, rotation, hasOrderItems: bool }`  
`ProcessedNodeItemDto`: `{ catalogItemWithCharacteristic: NodeCharacteristicDto, count: int }`

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

**Duplicate validation** (both `POST` and `PUT`):
- 422 `catalogItemArticleDuplicate` — field `article`: another catalog item with the same article already exists
- 422 `catalogItemBarcodeDuplicate` — field `barcode`: another catalog item with the same barcode already exists
- 422 `catalogItemCharacteristicDuplicate` — field `characteristics[i].characteristic`: duplicate characteristic name within the request
- 422 `catalogItemCharacteristicBarcodeDuplicate` — field `characteristics[i].barcode`: characteristic barcode already exists globally (or appears more than once in the request)

**Key DTOs:**

`CatalogItemSummaryDto`: `{ id, name, article, barcode?, characteristicCount }`  
`CatalogItemDto`: `{ id, name, article, barcode?, characteristics: CatalogItemCharacteristicDto[] }`  
`CatalogItemCharacteristicDto`: `{ id, characteristic, barcode? }`

---

## Permissions — `/api/permissions`

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| GET | `/api/permissions` | Bearer | All defined permission strings |