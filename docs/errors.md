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

`code` is a camelCase string matching the `ErrorCode` enum. `detail` is `"code: human-readable message"`. `args` is an optional object with extra context for i18n message formatting — its shape depends on the error code (see [ErrorCode Reference](#errorcode-reference)). `null` when no extra context is needed.

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
| Code | When | `args` |
|------|------|--------|
| `permissionDenied` | User lacks the required permission | — |
| `roleProtected` | Attempt to delete, rename, or remove permissions from the Admin role | `{ roleName: string }` |

### Entities
| Code | When |
|------|------|
| `userNotFound` | User ID not found |
| `roleNotFound` | Role ID not found |
| `permissionNotFound` | Permission string not in `Permissions.All` |
| `userAlreadyExists` | Username already taken |
| `roleAlreadyExists` | Role name already taken |
| `permissionAlreadyAssigned` | Permission already on the role/user |
| `warehouseNotFound` | Warehouse ID not found |
| `storagePlaceNotFound` | Storage place ID not found or does not belong to this warehouse |
| `catalogItemNotFound` | Catalog item ID not found |
| `catalogItemCharacteristicNotFound` | Characteristic ID not found or does not belong to this catalog item |
| `catalogItemCharacteristicDuplicate` | The same `catalogItemWithCharacteristicId` appears more than once in a node items sync request |
| `storagePlaceNodeNotFound` | Node ID not found or does not belong to this storage place |
| `storagePlaceNodeHasChildren` | Attempt to delete a node that still has child nodes |
| `storagePlaceNodeHasItems` | Attempt to delete a node that has items stored in it |
| `storagePlaceNodeParentHasItems` | Attempt to add a child node to a parent that already has items stored in it |
| `storagePlaceNodeCyclicParent` | Setting the requested parent would create a cycle (parent is a descendant of the node, or the node itself) |
| `storagePlaceNodeItemsGroupNotFound` | Item group ID not found or does not belong to this node |
| `catalogItemIsInUse` | Attempt to delete a catalog item that is currently stored in a warehouse |
| `catalogItemIsImmutable` | Attempt to change a CatalogItem's type |
| `catalogItemManagedByGroup` | Attempt to update a ProductGroup child directly — must go through the group |
| `catalogItemGroupInvalid` | `groupId` does not point to an existing ProductGroup |
| `catalogItemVariationInvalid` | A variation or member ID is invalid or wrong type |
| `catalogItemComponentInvalid` | A bundle component ID does not exist or has a type not allowed as a component |
| `catalogItemComponentNotFound` | A bundle component update references an ID that does not belong to this bundle |
| `catalogItemCircularDependency` | Saving the Bundle or Variation would create a cycle in the Bundle↔Variation nesting graph |
| `catalogItemArticleDuplicate` | Another catalog item with the same article already exists |
| `catalogItemBarcodeDuplicate` | Another catalog item with the same barcode already exists |
| `catalogItemCharacteristicBarcodeDuplicate` | A characteristic with this barcode already exists (globally, or duplicate within the request) |
| `unitInventoryItemNotFound` | Unit inventory item ID not found (or already deleted) |
| `inventoryItemMovedToAnotherNodeAfterPlacementCreated` | Item was moved to a different node after the placement was created — refresh and retry |
| `transferSameNode` | `fromNodeId` and `toNodeId` are the same node |
| `transferNotAssignedToWarehouse` | Current user is not assigned to the source or the destination warehouse (assigned permission check); the message names which side |
| `warehouseNotAssigned` | Current user is not assigned to this warehouse (assigned permission check) |
| `storagePlaceNotAssignedToWarehouse` | Current user is not assigned to the warehouse the storage place belongs to (assigned permission check) |
| `receiptNotFound` | Receipt ID not found |
| `receiptInvalidStatusTransition` | Operation not allowed for the current receipt status |
| `receiptHasPlacements` | Revert/cancel blocked because one or more items already have placements |
| `receiptItemNotFound` | Receipt item ID not found or does not belong to this receipt |
| `receiptItemPlacementNotFound` | Placement ID not found or does not belong to this item |
| `receiptNotAssignedToWarehouse` | Current user is not assigned to the receipt's warehouse (assigned permission check) |
| `receiptItemsUnderplaced` | Finish blocked: some items with `receivedCount` have fewer placements than required |
| `receiptItemsOverplaced` | Finish blocked: some items with `receivedCount` have more placements than the received count |
| `assemblyComponentAlreadyFulfilled` | Adding a fulfillment (single or batch) to an `AssemblyTaskBoxComponent` that is already fully fulfilled — guards against double-submit re-processing the same request |
| `catalogItemNotVariationMember` | The item picked for a Variation component is not one of its members (nested variations are walked through). Field `resolvedCatalogItemId` for Standard/Bundle, `unitInventoryItemId` for Unit — there the item's own catalog entry is checked |
| `stocktakeNotFound` | Stocktake ID not found |
| `stocktakeInvalidStatusTransition` | Operation not allowed for the current stocktake status (edit lines outside InProgress, delete outside Draft, finish outside InProgress, cancel from a terminal status) |
| `stocktakeNotAssignedToWarehouse` | Current user is not assigned to the stocktake's warehouse (assigned permission check) |
| `stocktakeHasNoNodes` | Start or finish blocked: no cells selected |
| `stocktakeNodeNotFound` | The storage node is not part of this stocktake's scope |
| `stocktakeNodeAlreadyInProgress` | The cell is already being counted in another **InProgress** stocktake — two running counts would fight at finish. Raised by `POST /start`, and by `PUT /nodes` when the scope of an already-started stocktake grows; a cell may sit in any number of Draft/Planned scopes. `args: { nodeId: string }` |
| `stocktakeUnitCountedTwice` | The same serial is claimed found twice — either in two cells of one document, or in another **InProgress** stocktake, where the finish order would decide where the unit lands and leave a phantom shortage in the loser. Checked on `PUT /nodes/{nodeId}/items`, surpluses included; `stocktakeUnitItemInAnotherWarehouse` takes precedence when both apply. `args: { inventoryNumber: string, stocktakeId?: string, stocktakeNumber?: number }` — the id is there so the UI can link the conflicting document for whoever has access to it |
| `stocktakeUnitItemInAnotherWarehouse` | A counted serial is booked in a different warehouse — a cross-warehouse move is a transfer decision, not a count. `args: { inventoryNumber: string }` |
| `stocktakeUnitItemDetached` | A found serial is detached and held by an active assembly fulfillment; reattaching it would steal it from the order being assembled |
| `stocktakeConcurrentModification` | Stock changed while the stocktake was being finished (a serial left its expected node) — the transaction rolled back, nothing was applied |

### Inventory
| Code | When | `args` |
|------|------|--------|
| `insufficientInventory` | Not enough Standard items in the source node — order fulfillment, transfer, or removing a receipt placement | `{ itemName: string, requested: number, available: number, missing: number, path: string }` |
| `writeoffInsufficientInventory` | Same shortage, raised while finishing a write-off | same as above |
| `inventoryItemNodeMismatch` | A Unit item is no longer in the node the operation expected (moved after the request was built) — order fulfillment and write-off finish | — |

Both are produced from a single throw site (`InventoryService.RemoveStandardItemsFromNodeAsync`), which resolves the
catalog item name and the node breadcrumb on the failure path. `path` is the breadcrumb joined with `" / "`
(e.g. `"Основной склад / Стеллаж A / Ячейка 3"`), `missing` is `requested - available`. The client formats a detailed
Russian message from these args and falls back to the plain per-code message when they are absent
(`errorCodeArgMessages` in `src/utils/errorUtils.ts`).

### Marketplaces
| Code | When | `args` |
|------|------|--------|
| `marketplaceAccountNotFound` | Marketplace account ID not found | `{ accountId: string }` when raised from a sync run |
| `marketplaceWarehouseNotFound` | Marketplace warehouse ID not found | — |
| `marketplaceCardNotFound` | Marketplace card ID not found | — |
| `marketplaceCredentialsInvalid` | The marketplace rejected `Client-Id` / `Api-Key` (401/403) | `{ marketplaceStatus: number, marketplaceResponse?: string }` |
| `marketplaceCredentialsUnreadable` | The stored key could not be decrypted — the Data Protection key ring was lost | — |
| `marketplaceClientIdRequired` | The provider declares `requiresClientId` and none was supplied | — |
| `marketplaceApiError` | The marketplace returned an error or is unreachable (502 over HTTP) | `{ marketplaceStatus: number, marketplaceResponse?: string }` |
| `marketplaceSyncAlreadyRunning` | A sync is already running for this account (409) | — |
| `marketplaceSyncInterrupted` | A run left in `running` by an application shutdown, reconciled on the next start | — |
| `marketplaceCardMappingTypeNotAllowed` | Attempt to map a card to a `ProductGroup` | — |
| `marketplaceCardMappingArchivedItem` | The target catalog item is archived. Only checked when **setting** a mapping — an item archived afterwards keeps its mapping | — |

`marketplaceResponse` is the marketplace's response body, truncated to 2000 characters — it is what makes a
rejection debuggable after the fact, so it is persisted with the failed sync run rather than only logged.
Request headers are never included anywhere in these errors — that is where the API key travels.

The last four codes are also persisted, not just returned: `MarketplaceSyncRun.Error` and
`MarketplaceAccount.LastSyncError` store a whole `AppFieldError` in a `jsonb` column, so a failed run keeps its
machine-readable `code` and `args` instead of a prose string. `ErrorCode` is serialized there as its **integer**
value (Npgsql's serializer, not the MVC one), which is why its numbers are pinned explicitly: a new code takes the
next free number and is declared where it belongs, but an existing member is never renumbered.

### DataFiles
| Code | When | `args` |
|------|------|--------|
| `dataFileNotFound` | File ID not found. Also returned when an entity is saved with a reference to a file the GC already collected — a form left open longer than `OrphanTtlHours` | — |
| `dataFileEmpty` | No file in the request, or zero bytes | — |
| `dataFileTooLarge` | Larger than `DataFiles:MaxFileSizeBytes` | `{ maxBytes: number }` |
| `dataFileTypeNotAllowed` | The declared content type is not allow-listed, or does not match what the leading bytes look like | `{ allowed: string }` |
| `dataFileNotAnImage` | A preview was requested for a non-image, or the image could not be decoded | — |
| `dataFileWidthNotAllowed` | `?width=` is not one of `DataFiles:ThumbnailWidths` | `{ allowed: string }` |
| `dataFileStorageError` | Bytes were stored but the metadata row could not be written; the bytes are removed again | — |

### FBS order sync

Declared with the marketplace block in `ErrorCode`, keeping the numbers they were first assigned: these values
persist as ints inside the `Error`, `LastSyncError` and `SkippedOrders` jsonb columns, so renumbering one would
reinterpret errors already stored.

| Code | When | `args` |
|------|------|--------|
| `marketplaceOrdersNotSupported` | The account's provider does not declare `orders` (or `labels`) | — |
| `marketplaceAccountHasOrders` | Deleting an account that has imported postings | — |
| `marketplaceAccountInactive` | A disabled account was ticked in the sync dialog; only in `failedItems` | — |
| `marketplaceLabelNotReady` | The marketplace has not printed some of the requested labels yet | `{ postingNumbers: string[], count: number }` |
| `marketplaceOrderNotFromMarketplace` | A label was requested for an order with no `MarketplaceOrder` | `{ orderIds: Guid[] }` |
| `marketplaceOrderNotAwaitingDeliver` | A label with no cached file was requested for a posting that is not awaiting shipment | `{ postingNumbers: string[], count: number }` |
| `marketplaceOrderCardNotMapped` | Posting skipped: an item has no card, or the card is not mapped to a catalog item. Only inside `SkippedOrders` | `{ offerIds: string }` |
| `marketplaceOrderWarehouseNotMapped` | Posting skipped: its warehouse is not mapped to a WMS warehouse. Only inside `SkippedOrders` | — |

`count` on `marketplaceLabelNotReady` duplicates the length of `postingNumbers` on purpose: the client's
`interpolateArgs` needs a scalar to pluralize, and an array does not.

A skipped posting is never silent. It is counted in `MarketplaceSyncRun.OrdersSkipped` and, for the first
100, described in `SkippedOrders` — an order that vanishes quietly is discovered at the warehouse when it
is already too late to ship.

`dataFileNotFound` on a **save** is the expected failure mode of upload-first, not a bug: the file was
uploaded, the form sat open past the TTL, and the collector took it. It is raised by an explicit existence
check rather than by the foreign key, because a raw `23503` would surface as a 500 that the client cannot render.

### Realtime

| Code | When | `args` |
|------|------|--------|
| `realtimeConnectionUnknown` | `watch` for a `connectionId` that does not exist, is already closed, or belongs to another user (field: `connectionId`) | — |

`unwatch` never raises it for a missing connection: a client unsubscribing after its stream already dropped
has nothing left to undo, and an error there would only fire on every page it leaves. It is still raised when
the connection exists but belongs to someone else.

Refusing a subscription for lack of rights reuses `permissionDenied` (403) rather than a realtime-specific
code. `IEntityAccessService` collapses the reason to a bool, and telling "no such object" apart from "no
right to it" is exactly what a subscription endpoint must not leak.

### Validation
| Code | When | `args` |
|------|------|--------|
| `required` | Field missing or null | — |
| `tooShort` | Value shorter than minimum | — |
| `tooLong` | Value longer than maximum | — |
| `invalidFormat` | Wrong type or format (e.g. string where int expected) | — |
| `outOfRange` | Numeric value out of allowed range | — |
| `invalidJson` | Request body is not valid JSON | — |
| `validationError` | Catch-all for unrecognized validation messages | — |

### Password validation
Returned on `POST /api/users` (create), and password reset/change flows.

| Code | When | `args` |
|------|------|--------|
| `passwordTooShort` | Password is shorter than the minimum length | `{ minimalLength: number }` |
| `passwordAtLeastOneDigit` | Password does not contain a digit | — |
| `passwordAtLeastOneUppercase` | Password does not contain an uppercase letter | — |
| `passwordAtLeastOneLowercase` | Password does not contain a lowercase letter | — |
| `passwordInvalid` | Current password is incorrect (change-password flow) | — |

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
