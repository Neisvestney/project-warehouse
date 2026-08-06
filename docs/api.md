# REST API Reference

Base URL: `https://localhost:7095` (dev) / configured host (prod)  
All requests/responses use `application/json`, except the file endpoints — see [Files](#files--apifiles),
which take `multipart/form-data` and return raw byte streams.  
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

`WarehouseDto` includes `defaultStoragePlaceNodeId: Guid?` — the default storage place node for this warehouse. `PUT /api/warehouses/{id}` accepts `defaultStoragePlaceNodeId: Guid?`; returns 422 `storagePlaceNotFound` (field: `defaultStoragePlaceNodeId`) if the node does not belong to this warehouse. Deleting the referenced node sets this field to `null` via cascade.

`GET /api/warehouses/{id}/default-node` — returns `StoragePlaceNodeDetailsDto` for the warehouse's default node (includes full breadcrumb `name: string[]`). Returns 404 `storagePlaceNodeNotFound` if no default node is set.

`StoragePlaceNodeDetailsDto.name` is now `string[]` — the full breadcrumb path (e.g. `["Стеллаж А", "Полка 1", "Ячейка 3"]`). Applies to both `GET .../nodes/{nodeId}` and the new default-node endpoint.

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
| POST | `/api/receipts/{id}/items/quick-add` | `receipts.edit` or `receipts.process_assigned` | Add a single catalog item to the receipt with `plannedCount=0`. Processing status only. Used when a new item is discovered while physically receiving goods. |
| PATCH | `/api/receipts/{id}/items/{itemId}/received-count` | `receipts.edit` or `receipts.process_assigned` | Update actually received count for one item. Processing status only. |

**`POST .../items/quick-add` body (`QuickAddReceiptItemRequest`):**

| Field | Type | Description |
|-------|------|-------------|
| `catalogItemId` | `Guid` | Must exist, not archived, not virtual (`productGroup`, `variation`, `bundle`), not already in the receipt |

Returns `ReceiptDto`. Errors: `catalogItemNotFound`, `catalogItemIsImmutable` (archived), `validationError` (virtual type or duplicate).

### Placements

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| POST | `/api/receipts/{id}/items/{itemId}/placements/standard` | `receipts.edit` or `receipts.process_assigned` | Place count-based (Standard) items at a storage node. Processing status only. |
| POST | `/api/receipts/{id}/items/{itemId}/placements/unit` | `receipts.edit` or `receipts.process_assigned` | Place a serialised Unit item (by `inventoryNumber`) at a storage node. Processing status only. |
| POST | `/api/receipts/{id}/placements/standard/batch` | `receipts.edit` or `receipts.process_assigned` | Place multiple Standard items at the same storage node in one transaction. Processing status only. |
| DELETE | `/api/receipts/{id}/items/{itemId}/placements/{placementId}` | `receipts.edit` or `receipts.process_assigned` | Remove a placement, reversing the inventory change. Processing status only. |

**`POST .../placements/standard/batch` body (`BatchStandardPlacementRequest`):**

| Field | Type | Description |
|-------|------|-------------|
| `storagePlaceNodeId` | `Guid` | Target storage node |
| `items` | `BatchStandardPlacementItemRequest[]` | One or more items to place (min 1, no duplicate `itemId`) |

`BatchStandardPlacementItemRequest`: `{ itemId: Guid, count: int (≥1) }`. All items must be of type `Standard`. Returns `ReceiptDto`.

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
`ReceiptItemPlacementDto`: `{ id, storagePlaceNodeId, storagePlaceName, storagePlacePath, count, unitInventoryItem?: ... }`

**`ReceiptReason` values:** `newGoods`, `return`, `other`  
**`ReceiptStatus` values:** `draft`, `planned`, `processing`, `finished`, `canceled`  
**`ReceiptSortBy` values:** `number` (default), `status`, `createdAt`, `warehouseName`, `name`, `plannedDeliveryDate`

---

## Transfers — `/api/transfers`

| Method | Path | Permission | Description |
|--------|------|------------|-------------|
| POST | `/api/transfers` | `transfers.execute` or `transfers.execute_assigned` | Execute an atomic inventory transfer between two storage nodes |

**Request body (`ExecuteTransferRequest`):**

| Field | Type | Description |
|-------|------|-------------|
| `fromNodeId` | `Guid` | Source storage place node |
| `toNodeId` | `Guid` | Destination storage place node (must differ from `fromNodeId`) |
| `items` | `TransferItemRequest[]` | One or more items to transfer (min 1) |

**`TransferItemRequest`** — type is inferred by which field is populated:

| Field | Type | Description |
|-------|------|-------------|
| `catalogItemId` | `Guid?` | Set for Standard items; requires `count` |
| `count` | `int?` | Required when `catalogItemId` is set (> 0) |
| `unitItemId` | `Guid?` | Set for Unit items |

**Permission notes:**
- `transfers.execute` — can transfer between any nodes
- `transfers.execute_assigned` — can only transfer between nodes in warehouses assigned to the current user

**Errors:**
- `transferSameNode` — `fromNodeId` == `toNodeId`
- `insufficientInventory` — not enough Standard items available in the source node (carries `args`, see [errors.md](errors.md#inventory))
- `storagePlaceNodeNotFound` — source or destination node not found
- `unitInventoryItemNotFound` — Unit item not found (or already moved)

All items are moved in a single DB transaction — any failure rolls back the entire operation.

---

## Writeoffs — `/api/writeoffs`

| Method | Path | Permission | Description |
|--------|------|------------|-------------|
| GET | `/api/writeoffs` | `writeoffs.view` or `writeoffs.view_assigned` | List write-offs (paginated) |
| GET | `/api/writeoffs/{id}` | `writeoffs.view` or `writeoffs.view_assigned` | Get full write-off with items |
| POST | `/api/writeoffs` | `writeoffs.edit` or `writeoffs.edit_assigned` | Create write-off in Draft status |
| PATCH | `/api/writeoffs/{id}` | `writeoffs.edit` or `writeoffs.edit_assigned` | Update name/reason/notes (Draft only) |
| DELETE | `/api/writeoffs/{id}` | `writeoffs.edit` or `writeoffs.edit_assigned` | Delete write-off (Draft only) |
| PUT | `/api/writeoffs/{id}/items` | `writeoffs.edit` or `writeoffs.edit_assigned` | Replace full items list (Draft only) |
| POST | `/api/writeoffs/{id}/finish` | `writeoffs.edit` or `writeoffs.edit_assigned` | Execute write-off: remove items from inventory (Draft → Finished) |
| POST | `/api/writeoffs/{id}/cancel` | `writeoffs.edit` or `writeoffs.edit_assigned` | Cancel write-off (Draft → Canceled) |

**Query parameters for `GET /api/writeoffs`:**

| Param | Type | Description |
|-------|------|-------------|
| `page` | `int` | Page number (default 1) |
| `pageSize` | `int` | Items per page (default 20, max 200) |
| `searchString` | `string?` | Search in number, name, notes |
| `warehouseId` | `Guid?` | Filter by warehouse |
| `status` | `WriteoffStatus?` | Filter by status |
| `reason` | `WriteoffReason?` | Filter by reason |
| `sortBy` | `WriteoffSortBy` | Sort field (default `number`) |
| `sortOrder` | `asc`\|`desc` | Sort direction (default `desc`) |

**`CreateWriteoffRequest`:**

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `name` | `string` | ✓ | Display name (max 256) |
| `reason` | `WriteoffReason` | ✓ | `loss` \| `defect` \| `other` |
| `warehouseId` | `Guid` | ✓ | Target warehouse |
| `notes` | `string?` | — | Free-text notes (max 2048) |

**`UpdateWriteoffRequest`:** same fields as Create (without `warehouseId`).

**`PUT /api/writeoffs/{id}/items` body — `WriteoffItemRequest[]`:**

Each element represents one inventory line. Exactly one item type discriminator must be set:

| Field | Type | Description |
|-------|------|-------------|
| `sourceNodeId` | `Guid` | Storage node to remove from (must belong to write-off's warehouse) |
| `catalogItemId` | `Guid?` | Standard item — also requires `count` |
| `count` | `int?` | Required when `catalogItemId` is set (> 0) |
| `unitInventoryItemId` | `Guid?` | Unit item ID (must be at `sourceNodeId`) |
| `notes` | `string?` | Line-level notes |

**Key DTOs:**

`WriteoffSummaryDto`: `{ id, number, name, reason, status, warehouseId, warehouseName, itemsCount, createdAt }`

`WriteoffDto`: same as summary + `notes?` + `items: WriteoffItemDto[]`

`WriteoffItemDto`: `{ id, sourceNodeId, sourceNodePath: string[], notes?, catalogItemId?, catalogItem?, count, unitInventoryItemId?, inventoryNumber?, catalogItemName }`

**`WriteoffReason` values:** `loss`, `defect`, `other`  
**`WriteoffStatus` values:** `draft`, `finished`, `canceled`  
**`WriteoffSortBy` values:** `number` (default), `name`, `status`, `createdAt`, `warehouseName`

**`POST /api/writeoffs/{id}/finish` behaviour:**

All item removals execute in a single DB transaction. If any operation fails, nothing is committed. Possible 422 errors:

- `writeoffNotDraft` — write-off is not in Draft status
- `writeoffHasNoItems` — no items to write off
- `writeoffInsufficientInventory` — not enough Standard items in the source node (carries `args`, see [errors.md](errors.md#inventory))
- `unitInventoryItemNotFound` — Unit item not found at expected node

**Permission notes:**
- `writeoffs.view` / `writeoffs.edit` — access all warehouses
- `writeoffs.view_assigned` / `writeoffs.edit_assigned` — restricted to user's assigned warehouses

---

## Catalog — `/api/catalog`

| Method | Path | Permission | Description |
|--------|------|------------|-------------|
| GET | `/api/catalog` | `catalog.view` | List catalog items paginated (`Paginated<CatalogItemSummaryDto>`), supports `searchString`, `sortBy` (`name`\|`article`\|`barcode`\|`type`, default `name`), `sortOrder` (`asc`\|`desc`, default `asc`), `itemTypes` (repeatable `CatalogItemType`, omit for all); archived items always sorted last |
| GET | `/api/catalog/{id}` | `catalog.view` | Get full catalog item details (`CatalogItemDto`) |
| GET | `/api/catalog/tags` | `catalog.view` | List tags (ordered by name), supports `search` query param |
| POST | `/api/catalog` | `catalog.edit` | Create catalog item |
| PUT | `/api/catalog/{id}` | `catalog.edit` | Update catalog item and atomically sync type-specific collections (children/components/variationIds/memberIds) |
| DELETE | `/api/catalog/{id}` | `catalog.edit` | Delete catalog item |

**Children sync rules** (ProductGroup only, `PUT /api/catalog/{id}` body: `UpdateCatalogItemRequest`):
- `id: null` → create new child item
- `id` present → update existing child
- existing child not in the list → delete

**Images** (`POST` accepts `mainImageFileId` only; `PUT` and each product group child accept both):
- `mainImageFileId: Guid?` — the item's own main image
- `images: [{ id: Guid?, fileId: Guid, order: number }]` — gallery; `id: null` creates the link, an existing
  link missing from the list is removed. Removing a link does not delete the file; the GC does that later.
- `CatalogItemDto.mainImage` is the **effective** image: the item's own, otherwise the parent group's, the same
  way `description` and `notes` inherit. `mainImageFileId` stays null when the shown image is inherited — that
  pair is how the UI tells "own" from "inherited". The `images` list is never inherited.
- 422 `dataFileNotFound` — a referenced file does not exist (see [errors.md](errors.md))

**Duplicate validation** (both `POST` and `PUT`):
- 422 `catalogItemArticleDuplicate` — field `article`
- 422 `catalogItemBarcodeDuplicate` — field `barcode`
- 422 `catalogItemComponentInvalid` — a component item is of an invalid type for bundles
- 422 `catalogItemVariationInvalid` — a variation ID is invalid or wrong type
- 422 `catalogItemGroupInvalid` — `groupId` does not refer to a ProductGroup
- 422 `catalogItemIsImmutable` — attempt to change a CatalogItem's type
- 422 `catalogItemManagedByGroup` — item with `groupId` cannot be edited directly
- 422 `catalogItemCircularDependency` — saving a Bundle or Variation would create a cycle in the Bundle↔Variation nesting graph

**`CatalogItemType` values:** `standard`, `unit`, `productGroup`, `variation`, `bundle`

**Key DTOs:**

`CatalogItemSummaryDto`: `{ id, type, name, fullName, article, barcode?, isArchived }`  
`CatalogItemDto`: `{ id, type, name, fullName, article, barcode?, description?, notes?, isArchived, groupId?, groupName?, tags: CatalogItemTagDto[], components: BundleComponentDto[], variationIds: Guid[], memberIds: Guid[], children: CatalogItemDto[] }`  
`CatalogItemTagDto`: `{ id, name }`  
`BundleComponentDto`: `{ id, componentId, componentName, quantity }`

---

## Inventory Items — `/api/inventory-items`

| Method | Path | Permission | Description |
|--------|------|------------|-------------|
| GET | `/api/inventory-items` | `warehouses.view` or `warehouses.view_assigned` | Paginated stock overview aggregated by catalog item |
| GET | `/api/inventory-items/units` | `warehouses.view` or `warehouses.view_assigned` | Paginated list of individual unit inventory item instances |

`view_assigned` limits results to warehouses assigned to the current user.

### `GET /api/inventory-items` — GetAll
Query params: `page`, `pageSize`, `searchString?`, `warehouseId?`, `storagePlaceId?`, `nodeId?`, `catalogItemType?` (CatalogItemType), `isArchived?` (bool)  
Returns: `Paginated<InventoryItemSummaryDto>`

Items from both storage mechanisms (StoragePlaceNodeItemsGroup for standard items, UnitInventoryItem) are counted separately per `CatalogItemId` and merged. The result is one row per catalog item with `Count` = total across all locations within the applied filters.

**`InventoryItemSummaryDto`**: `{ catalogItemId: Guid, catalogItem: CatalogItemSummaryDto, count: int }`

### `GET /api/inventory-items/units` — GetAllUnits
Query params: `page`, `pageSize`, `searchString?` (searches SKU), `warehouseId?`, `storagePlaceId?`, `nodeId?`, `catalogItemId?`  
Returns: `Paginated<UnitInventoryItemDto>`

The `catalogItemId` filter is used by the frontend drawer to list all instances of a clicked catalog item.

**`UnitInventoryItemDto`**: `{ id: Guid, sku: string, catalogItem: CatalogItemSummaryDto, warehouseId: Guid, warehouseName: string, storagePlaceId: Guid, storagePlaceName: string, nodeId: Guid, nodeName: string }`

---

## Files — `/api/files`

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| POST | `/api/files` | Bearer | Upload one file (`multipart/form-data`, field `file`) → `DataFileDto` |
| GET | `/api/files/{id}` | Bearer | Metadata → `DataFileDto` |
| GET | `/api/files/{id}/content` | Bearer | The original bytes |
| GET | `/api/files/{id}/thumbnail?width=` | Bearer | Downscaled preview, images only, always `image/webp` |

**`DataFileDto`**: `{ id: Guid, originalFileName: string, contentType: string, sizeBytes: number, imageWidth: number?, imageHeight: number?, isImage: bool, createdById: Guid?, createdByUserName: string?, createdAt: DateTime }`

No permission beyond `[Authorize]`: the right to attach a file is the right to edit the owning entity, and
that is already checked on the entity's own endpoint. See [data-files-specification.md](data-files-specification.md)
for the known limitation this leaves.

**There is no delete endpoint.** The only way to remove a file is to drop the reference to it, after which the
garbage collector takes it. That makes "entity points at a deleted file" unreachable.

`width` must be one of `DataFiles:ThumbnailWidths` (default `64, 128, 256, 512, 1024`); arbitrary values are
rejected so the disk cache cannot be flooded. An original narrower than the request is returned as-is.

Responses carry `X-Content-Type-Options: nosniff`, an `ETag` derived from the ID (content at an ID is
immutable), and support range requests. Only `image/jpeg|png|webp|gif` and `application/pdf` are served
inline; everything else gets `Content-Disposition: attachment`. `image/svg+xml` is allow-listed nowhere —
an SVG is a scriptable document and inline from our own origin it is stored XSS.

---

## System — `/api/system`

| Method | Path | Permission | Description |
|--------|------|------------|-------------|
| GET | `/api/system/storage` | `system.view` | File storage usage → `StorageStatsDto` |
| GET | `/api/system/database` | `system.view` | Database size by entity type → `DatabaseStatsDto` |

**`StorageStatsDto`**: `{ fileCount, totalSizeBytes, byContentType: [{ contentType, count, sizeBytes }], largestFiles: [{ id, originalFileName, contentType, sizeBytes, createdAt }], orphanCount, orphanSizeBytes, orphanDueCount, orphanDueSizeBytes, thumbnailCacheSizeBytes, orphanTtlHours, diskStatsAt: DateTime?, disk: { mountPoint, totalBytes, freeBytes, usedBytes }? }`

`orphanCount` is every file no foreign key points at; `orphanDueCount` is the subset already past
`OrphanTtlHours` — what the next GC run will actually take. `disk` is null when the mount point could not be
resolved. `thumbnailCacheSizeBytes` and `disk` come from a disk walk cached for `DataFiles:StatsCacheSeconds`;
`diskStatsAt` says when they were measured.

**`DatabaseStatsDto`**: `{ totalSizeBytes, tablesSizeBytes, byEntityType: [{ entityType: AppEntityType, sizeBytes, tableSizeBytes, indexSizeBytes, rowEstimate: long?, tables: [{ name, sizeBytes, tableSizeBytes, indexSizeBytes, rowEstimate: long? }] }] }`

Sizes come from `pg_total_relation_size` (heap + TOAST + indexes) over `pg_class`, so `totalSizeBytes`
(`pg_database_size`) exceeds `tablesSizeBytes` by Postgres' own catalogs. `rowEstimate` is
`pg_class.reltuples` — the planner's estimate, **not** a count; it is null for a table that has never been
analysed, and null for a group where no table has. Grouping is `Infrastructure/EntityTypeTables.cs`, which
maps CLR entity types to `AppEntityType` and reads table names off the EF model; anything unmapped falls into
`unknown` and is shown as «Прочее».

---

## Permissions — `/api/permissions`

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| GET | `/api/permissions` | Bearer | All defined permission strings |
---

## Marketplaces — `/api/integrations/marketplaces`

| Method | Path | Permission | Description |
|--------|------|------------|-------------|
| GET | `/accounts` | `integrations.view` | List accounts (`Paginated<MarketplaceAccountSummaryDto>`), supports `searchString`, `type`, `isActive`, `sortBy` (`name`\|`createdAt`\|`lastSyncAt`, default `name`), `sortOrder` |
| GET | `/accounts/unmapped-count` | `integrations.view` | `{ count }` — unmapped, non-archived cards across all **active** accounts; feeds the sidebar badge |
| GET | `/accounts/{id}` | `integrations.view` | Account with aggregates (`MarketplaceAccountDto`) |
| POST | `/accounts` | `integrations.edit` | Create account; returns `201` and queues an initial `all` sync when `isActive` |
| PUT | `/accounts/{id}` | `integrations.edit` | Update account; an empty `apiKey` keeps the stored key |
| DELETE | `/accounts/{id}` | `integrations.edit` | Delete account; cascades to its warehouses, cards and sync runs |
| POST | `/accounts/{id}/test-connection` | `integrations.edit` | Verify credentials without saving |
| POST | `/accounts/{id}/sync` | `integrations.map` | Queue a sync → `202` + `{ syncRunId }` |
| GET | `/accounts/{id}/sync-runs` | `integrations.view` | Run history (`Paginated<MarketplaceSyncRunDto>`), newest first |
| GET | `/accounts/{id}/warehouses` | `integrations.view` | Marketplace warehouses, supports `includeArchived`, `sortBy` (`name`\|`kind`\|`syncedAt`), `sortOrder` |
| PUT | `/warehouses/{id}/mapping` | `integrations.map` | Map to a WMS warehouse |
| GET | `/accounts/{id}/cards` | `integrations.view` | Cards, supports `searchString`, `mappingState`, `includeArchived`, `sortBy` (`name`\|`offerId`\|`price`\|`syncedAt`), `sortOrder` |
| PUT | `/cards/{id}/mapping` | `integrations.map` | Map to a catalog item |
| POST | `/accounts/{id}/cards/auto-map` | `integrations.map` | Auto-map the whole account → `{ mapped, remaining }` |

**`POST /accounts` body (`CreateMarketplaceAccountRequest`):**

| Field | Type | Description |
|-------|------|-------------|
| `type` | `MarketplaceType` | `ozon` (only provider implemented) |
| `clientId` | `string?` | Required when the provider declares `requiresClientId` (Ozon does) |
| `apiKey` | `string` | Write-only; stored encrypted, never returned |
| `syncIntervalMinutes` | `int?` | 1…10080, defaults to `Marketplaces:DefaultSyncIntervalMinutes` |
| `isActive` | `bool` | Inactive accounts are skipped by the scheduler |

`PUT /accounts/{id}` takes the same shape minus `type`; `apiKey` there is optional and an empty value means "keep the current key".

Neither body accepts a `name`: the account name comes from the marketplace (`company.name` for Ozon) and every sync overwrites it, along with `companyLegalName`, `inn`, `ogrn` and `ownershipForm`. Until the first sync lands, `name` holds a placeholder built from the marketplace and the key mask (`Ozon ••••1234`).

**`POST /accounts/{id}/test-connection` body (`TestConnectionRequest`):** `{ type?, clientId?, apiKey? }`. When `apiKey` is present the route `{id}` is ignored entirely, so a key can be checked before the account exists — the path segment may be any string. Otherwise the saved credentials of `{id}` are used.

**Mapping bodies:** `PUT /warehouses/{id}/mapping` takes `{ warehouseId: Guid? }`, `PUT /cards/{id}/mapping` takes `{ catalogItemId: Guid? }`. `null` clears the mapping.

**`mappingState` values:** `all` (default), `unmapped`, `mapped`, `archivedItem` (mapped to a catalog item that has since been archived).

**Enum values:** `MarketplaceType`: `ozon`, `wildberries` (reserved). `MarketplaceWarehouseKind`: `unknown`, `fbs`, `rfbs`, `express`, `fbo`. `MarketplaceMappingSource`: `manual`, `autoOfferId`, `autoBarcode`. `MarketplaceSyncScope`: `warehouses`, `cards`, `all`. `MarketplaceSyncStatus`: `running`, `success`, `failed`, `canceled` (reserved, unused).

**Key DTOs:**

`MarketplaceAccountSummaryDto`: `{ id, type, name, isActive, syncIntervalMinutes, lastSyncAt?, lastSyncStatus?, lastSyncError?, warehouseCount, cardCount, unmappedCardCount }`
`MarketplaceAccountDto`: `{ id, type, name, isActive, externalClientId?, companyLegalName?, inn?, ogrn?, ownershipForm?, apiKeyLast4, apiKeyUpdatedAt?, credentialsUnreadable, capabilities, syncIntervalMinutes, lastSyncAt?, lastSyncStatus?, lastSyncError?, createdAt, createdById?, createdByName?, warehouseCount, unmappedWarehouseCount, cardCount, unmappedCardCount }`
`MarketplaceWarehouseDto`: `{ id, marketplaceAccountId, externalId, name, kind, externalStatus?, address?, isArchived, warehouseId?, warehouseName?, syncedAt }`
`MarketplaceCardDto`: `{ id, marketplaceAccountId, externalId, sku?, offerId, name, barcodes, primaryImageUrl?, price?, currencyCode?, isArchived, catalogItemId?, catalogItemFullName?, catalogItemArticle?, mappingSource?, mappedAt?, isMappedToArchivedItem, syncedAt }`
`MarketplaceSyncRunDto`: `{ id, marketplaceAccountId, scope, status, startedAt, finishedAt?, triggeredById?, triggeredByName?, warehousesProcessed, cardsProcessed, cardsCreated, cardsUpdated, cardsArchived, autoMapped, error? }`

**The API key never leaves the server.** `MarketplaceAccountDto` has no key field at all — only `apiKeyLast4` (the key tail; the client renders it as `••••1234`) and `apiKeyUpdatedAt`. `credentialsUnreadable` is computed per request by attempting to decrypt the stored key; it turns `true` when the Data Protection key ring has been lost.

**`lastSyncError` / `MarketplaceSyncRunDto.error` are `AppFieldError`**, not strings: `{ code, detail, args? }`, the same shape used inside `AppProblemDetails`. Clients render from `code` + `args` (`marketplaceStatus`, `marketplaceResponse`, `accountId`) — `detail` is developer-facing English.

Errors: `marketplaceAccountNotFound`, `marketplaceWarehouseNotFound`, `marketplaceCardNotFound`, `marketplaceCredentialsInvalid`, `marketplaceCredentialsUnreadable`, `marketplaceClientIdRequired`, `marketplaceApiError`, `marketplaceSyncAlreadyRunning`, `marketplaceCardMappingTypeNotAllowed`, `marketplaceCardMappingArchivedItem`, `marketplaceSyncInterrupted`.
