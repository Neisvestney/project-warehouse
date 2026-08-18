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
| `users.edit_profile` | `Permissions.Users.EditProfile` |
| `users.delete` | `Permissions.Users.Delete` |
| `users.manage_roles_and_permissions` | `Permissions.Users.ManageRolesAndPermissions` |
| `users.manage_assigned_warehouses` | `Permissions.Users.ManageAssignedWarehouses` |
| `users.reset_password` | `Permissions.Users.ResetPassword` |

### Roles (`roles.*`)
| Permission | Constant |
|-----------|----------|
| `roles.view` | `Permissions.Roles.View` |
| `roles.edit` | `Permissions.Roles.Edit` |

### Warehouses (`warehouses.*`)
| Permission | Constant | Scope |
|-----------|----------|-------|
| `warehouses.view` | `Permissions.Warehouses.View` | All warehouses |
| `warehouses.edit` | `Permissions.Warehouses.Edit` | All warehouses |
| `warehouses.view_assigned` | `Permissions.Warehouses.ViewAssigned` | Only user's `AssignedWarehouses` |
| `warehouses.edit_assigned` | `Permissions.Warehouses.EditAssigned` | Only user's `AssignedWarehouses` |

`warehouses.view` и `warehouses.view_assigned` можно назначать одновременно — `view` всегда перекрывает `view_assigned`. Аналогично для `edit` / `edit_assigned`.

Эти же права скоупят всё, что физически лежит на складе и не имеет собственных прав: инвентарь
(`/api/inventory-items`) и места хранения с ячейками (`/api/storagePlaces`). Ячейки раньше требовали
безусловного `warehouses.edit` и `_assigned`-вариант игнорировали — теперь пользователь с
`warehouses.edit_assigned` правит ячейки своих складов и получает `storagePlaceNotAssignedToWarehouse` на чужих.
Ролям, у которых есть только `_assigned`-права, это открывает раздел ячеек, который им раньше был недоступен.

### Receipts (`receipts.*`)
| Permission | Constant | Scope |
|-----------|----------|-------|
| `receipts.view` | `Permissions.Receipts.View` | All receipts |
| `receipts.edit` | `Permissions.Receipts.Edit` | All receipts |
| `receipts.view_assigned` | `Permissions.Receipts.ViewAssigned` | Receipts in user's assigned warehouses |
| `receipts.edit_assigned` | `Permissions.Receipts.EditAssigned` | Receipts in user's assigned warehouses |
| `receipts.process_assigned` | `Permissions.Receipts.ProcessAssigned` | Placement ops in user's assigned warehouses |

`receipts.view` перекрывает `receipts.view_assigned`, `receipts.edit` перекрывает `receipts.edit_assigned`.  
`receipts.process_assigned` — право для операторов склада (приёмка физического товара: обновление `receivedCount` и добавление placements). Без `edit`/`edit_assigned` оператор видит только приёмки в статусе Processing своих складов.

### Writeoffs (`writeoffs.*`)
| Permission | Constant | Scope |
|-----------|----------|-------|
| `writeoffs.view` | `Permissions.Writeoffs.View` | All warehouses |
| `writeoffs.edit` | `Permissions.Writeoffs.Edit` | All warehouses |
| `writeoffs.view_assigned` | `Permissions.Writeoffs.ViewAssigned` | Only user's assigned warehouses |
| `writeoffs.edit_assigned` | `Permissions.Writeoffs.EditAssigned` | Only user's assigned warehouses |

`writeoffs.view` перекрывает `writeoffs.view_assigned`, `writeoffs.edit` перекрывает `writeoffs.edit_assigned`.

### Stocktakes (`stocktakes.*`)
| Permission | Constant | Scope |
|-----------|----------|-------|
| `stocktakes.view` | `Permissions.Stocktakes.View` | All warehouses |
| `stocktakes.edit` | `Permissions.Stocktakes.Edit` | All warehouses |
| `stocktakes.view_assigned` | `Permissions.Stocktakes.ViewAssigned` | Only user's assigned warehouses |
| `stocktakes.edit_assigned` | `Permissions.Stocktakes.EditAssigned` | Only user's assigned warehouses |

`stocktakes.view` перекрывает `stocktakes.view_assigned`, `stocktakes.edit` перекрывает `stocktakes.edit_assigned`.

Складских прав экран пересчёта не требует: остаток ячейки отдаёт `GET /api/stocktakes/{id}/nodes/{nodeId}/stock`, а не эндпоинты инвентаря (которые требуют `warehouses.view`). Роль «только инвентаризация» работоспособна без доступа к складам.

Права новые, поэтому после деплоя существующим ролям их надо выдать вручную — `Permissions.All` собирается рефлексией, и `DbSeeder` раздаёт всё только роли Admin.

### Catalog (`catalog.*`)
| Permission | Constant |
|-----------|----------|
| `catalog.view` | `Permissions.Catalog.View` |
| `catalog.edit` | `Permissions.Catalog.Edit` |

### Integrations (`integrations.*`)
| Permission | Constant |
|-----------|----------|
| `integrations.view` | `Permissions.Integrations.View` |
| `integrations.edit` | `Permissions.Integrations.Edit` |
| `integrations.map` | `Permissions.Integrations.Map` |

### System (`system.*`)
| Permission | Constant |
|-----------|----------|
| `system.view` | `Permissions.System.View` |

Instance-wide technical readouts (the «Хранилище» settings section), not a business area. There is deliberately no `system.manage`: nothing needs it yet, and an unused permission is a checkbox in the roles matrix that grants nothing. Add it together with the first action that requires it.

Uploading and reading files needs no permission of its own — see [api.md](api.md#files--apifiles).

`edit` and `map` are split on purpose: a merchandiser maps cards and warehouses and triggers syncs, while touching
API keys (create/update/delete an account, test a connection) stays with administrators.

**Grant `integrations.map` together with `catalog.view` and `warehouses.view`.** The mapping pickers are the shared
`CatalogItemsSelect` / `WarehousesSelect` components, which read `/api/catalog/for-select` and `/api/warehouses`;
without those two permissions the dropdowns return 403 and the mapping screens are unusable.

There are no `_assigned` variants: a marketplace account belongs to the shop as a whole rather than to a warehouse,
so scoping by `AssignedWarehouses` would be meaningless.

## Where Access Is Checked

Three layers, one predicate. All per-object checks go through **`Infrastructure/Access/`**; nothing hand-rolls a claim
pair plus an assigned-warehouse lookup any more.

| Layer | Used by | Call |
|---|---|---|
| Row filter | lists, global search, calendar (`IUserQueryFilterService`) | `rule.QueryAsync(user, level)` → `IQueryable<T>` |
| Loaded entity | controllers, before update/delete | `rule.CheckAsync(user, level, entity)` → `AccessVerdict` |
| Warehouse only | `Create`, where the entity does not exist yet | `rule.CheckWarehouseAsync(user, level, warehouseId)` |
| Permission only | the prelude of a list endpoint (403/401 instead of an empty page) | `rule.PrecheckAsync(user, level)` |
| By id | realtime `watch` and lock acquisition (`IEntityAccessService`) | `rule.CanAsync(user, level, id)` → `bool` |

`AccessVerdict` carries the refusal reason and the entity's own error code, so a controller writes
`AccessError(verdict)` instead of choosing between `PermissionDenied`, `*NotAssignedToWarehouse` and `TokenInvalid`
by hand. The refusal for "not your warehouse" is now the same code on the view and the edit path.

`AccessScope` is scoped to the request and memoises the assigned-warehouse set — it used to be re-queried by every
individual check. Where a query cannot be expressed as a rule predicate (inventory counts nested in subqueries,
a transfer spanning two warehouses), ask it for a `WarehouseNarrowing` instead: it separates "sees everything"
from "unusable token" so the caller does not have to re-read the permission to tell them apart.

### Adding a rule for a new entity

Register it in `EntityAccessRegistry` — that constructor is the entire entity-type → permission map:

```csharp
new WarehouseScopedRule<Writeoff>(db, scope, AppEntityType.Writeoff,
    viewAll: [Permissions.Writeoffs.View],   viewAssigned: [Permissions.Writeoffs.ViewAssigned],
    editAll: [Permissions.Writeoffs.Edit],   editAssigned: [Permissions.Writeoffs.EditAssigned],
    warehouse: w => w.WarehouseId,
    ErrorCode.WriteoffNotAssignedToWarehouse,
    "You are not assigned to the warehouse of this write-off.")
```

Use `SimpleAccessRule<T>` when the entity has no warehouse scope (catalog, users, roles, marketplace accounts).
Permissions are lists because several can behave identically — `orders.assemble_assigned` grants the same view as
`orders.view_assigned`. An entity type with **no** registered rule is inaccessible: realtime cannot subscribe to it
and every filter returns nothing.

Permissions that authorise an *action* rather than access to an object — `orders.self_assign`,
`orders.assemble_assigned`, `receipts.process_assigned`, `transfers.*`, `users.manage_*` — stay in their controllers.
They are not "may this user see this object".

## RBAC + Direct Permissions

A user's **effective permissions** = union of:
1. All permissions assigned to any of the user's roles (`RolePermission` table)
2. Any permissions assigned directly to the user (`UserPermission` table)

These are embedded in the JWT as individual `permission` claims. The `PermissionAuthorizationHandler` checks `User.HasClaim("permission", requiredPermission)` — pure in-memory, no DB access per request.

## Managing via API

See [Auth docs](auth.md) for getting a token first.

**Manage role permissions** — atomically replaces the entire roles collection including their permission sets:
```http
PUT /api/roles
Authorization: Bearer ...
Content-Type: application/json

[{ "id": "<guid>", "name": "Manager", "order": 1, "permissions": ["warehouses.view", "catalog.view"] }]
```
Requires `roles.edit`. See [api.md](api.md) for the full shape.

**Manage user direct permissions and roles** — atomically updates a user's profile, roles, and direct permissions:
```http
PUT /api/users/{id}
Authorization: Bearer ...
Content-Type: application/json

{ "roleIds": ["<guid>"], "directPermissions": ["users.view"] }
```
Requires `users.manage_roles_and_permissions` to change roles/permissions. See [api.md](api.md) for the full shape.

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

New permissions added to the code **are** granted to the Admin role automatically: `DbSeeder.SeedAsync` runs on every startup and inserts `Permissions.All.Except(existing)` for that role. No migration step is needed.

## Token Invalidation on Permission Change

When role or user permissions change, `SecurityVersionStore.BumpAsync(userId)` is called for all affected users. Their existing JWTs become invalid — the client must refresh. See [auth.md](auth.md) for the full TOKEN_OUTDATED flow.
