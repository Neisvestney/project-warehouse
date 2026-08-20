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

## The permission list

There is no list here. `Infrastructure/Permissions.cs` is the complete, authoritative set — nested static classes
by module, gathered into `Permissions.All` by reflection. `GET /api/permissions` returns the same set at runtime,
and each endpoint's XML `<remarks>` names the permission it requires.

What follows is only the part the constants cannot tell you.

## The `_assigned` convention

A permission suffixed `_assigned` grants the same operation as its unsuffixed twin, narrowed to the warehouses in
the caller's `AssignedWarehouses`. It reads the same across `view_assigned`, `edit_assigned`, `process_assigned`
and `execute_assigned`.

The broad form always overrides the narrow one, so holding both is harmless — grant the pair when a role is being
widened gradually. An entity outside the assigned set is not hidden behind a 404: read endpoints filter it out of
lists and return 403 on a direct fetch.

Warehouse permissions scope everything that physically sits in a warehouse and has no permission family of its
own — inventory (`/api/inventory-items`) and storage places with their cells (`/api/storagePlaces`). A user with
`warehouses.edit_assigned` edits cells of their own warehouses and gets `storagePlaceNotAssignedToWarehouse` on
anyone else's.

## Access rules worth knowing

**`receipts.process_assigned`** is the warehouse-floor permission: receiving physical goods, meaning
`receivedCount` updates and placements. Without `edit` or `edit_assigned`, its holder sees only Processing
receipts of their own warehouses.

**Assembly is warehouse-bound for everyone**, including holders of the unscoped `orders.edit` — physically
picking stock requires being assigned to the warehouse the stock sits in.

**Own-record access.** `GET /api/users/{id}` is allowed without `users.view` when `id` is the caller's own.
Otherwise every screen showing "who am I" would need a permission that also exposes the whole staff list.

**The stocktake counting screen needs no warehouse permission.** Per-cell stock comes from
`GET /api/stocktakes/{id}/nodes/{nodeId}/stock`, not from the inventory endpoints, which makes a
counting-only role workable without access to warehouses.

**Transfers have no entity**, so no rule in `EntityAccessRegistry` — the check lives in the controller and only
the assigned-warehouse narrowing is shared.

**Integrations split `edit` from `map` on purpose:** a merchandiser maps cards and warehouses and triggers syncs,
while touching API keys (create, update, delete an account, test a connection) stays with administrators. Grant
`integrations.map` together with `catalog.view` and `warehouses.view` — the mapping pickers are the shared
`CatalogItemsSelect` / `WarehousesSelect` components, and without those the dropdowns 403 and the screens are
unusable. There are no `_assigned` variants: an account belongs to the shop rather than to a warehouse.

**`system.*` is instance-wide technical readout**, not a business area. There is deliberately no `system.manage`:
an unused permission is a checkbox in the roles matrix that grants nothing. Add it with the first action needing it.

**Uploading and reading files needs no permission** — the right to attach a file is the right to edit the owning
entity, already checked on that entity's endpoint. See
[data-files-specification.md](data-files-specification.md) for the limitation this leaves.

## Where Access Is Checked

Three layers, one predicate. Every per-object check goes through **`Infrastructure/Access/`** — a claim pair plus
an assigned-warehouse lookup is never hand-rolled at a call site.

| Layer | Used by | Call |
|---|---|---|
| Row filter | lists, global search, calendar (`IUserQueryFilterService`) | `rule.QueryAsync(user, level)` → `IQueryable<T>` |
| Loaded entity | controllers, before update/delete | `rule.CheckAsync(user, level, entity)` → `AccessVerdict` |
| Warehouse only | `Create`, where the entity does not exist yet | `rule.CheckWarehouseAsync(user, level, warehouseId)` |
| Permission only | the prelude of a list endpoint (403/401 instead of an empty page) | `rule.PrecheckAsync(user, level)` |
| By id | realtime `watch` and lock acquisition (`IEntityAccessService`) | `rule.CanAsync(user, level, id)` → `bool` |
| By empty id | the same, for entities versioned as a whole (roles) | `rule.PrecheckAsync(user, level).Allowed` |

`AccessVerdict` carries the refusal reason and the entity's own error code, so a controller writes
`AccessError(verdict)` instead of choosing between `PermissionDenied`, `*NotAssignedToWarehouse` and `TokenInvalid`
by hand. The refusal for "not your warehouse" is the same code on the view and the edit path.

`AccessScope` is scoped to the request and memoises the assigned-warehouse set, so it is read once rather than per
check. Where a query cannot be expressed as a rule predicate (inventory counts nested in subqueries,
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

See [api.md](api.md) for getting a token first.

**Manage role permissions** — atomically replaces the entire roles collection including their permission sets:
```http
PUT /api/roles
Authorization: Bearer ...
Content-Type: application/json

[{ "id": "<guid>", "name": "Manager", "order": 1, "permissions": ["warehouses.view", "catalog.view"] }]
```
Requires `roles.edit`.

**Manage user direct permissions and roles** — atomically updates a user's profile, roles, and direct permissions:
```http
PUT /api/users/{id}
Authorization: Bearer ...
Content-Type: application/json

{ "roleIds": ["<guid>"], "directPermissions": ["users.view"] }
```
Requires `users.manage_roles_and_permissions` to change roles/permissions.

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

When role or user permissions change, `SecurityVersionStore.BumpAsync(userId)` is called for all affected users. Their existing JWTs become invalid — the client must refresh. See [api.md](api.md#securityversion--token-invalidation) for the full flow.
