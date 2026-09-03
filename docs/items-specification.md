# Items Specification

## Overview

`CatalogItem` describes a product — it is a catalog entry, not a physical inventory record. Physical storage is handled by separate entities depending on the item type.

---

## Item Types

| Type | Enum | Virtual | Stored via |
|------|------|---------|------------|
| Standard | `Standard` | No | `ItemsGroup` (count per node) |
| Unit | `Unit` | No | `UnitInventoryItem` (one record per physical unit) |
| Product Group | `ProductGroup` | Yes | — |
| Variation | `Variation` | Yes | — |
| Bundle | `Bundle` | Yes | — |

Virtual types (ProductGroup, Variation, Bundle) exist only in the catalog. They group or describe items but are never directly stored in a warehouse node.

---

## Type Descriptions

### Standard

A bulk item stored by count. Each distinct Standard CatalogItem in a warehouse node is tracked as one `ItemsGroup` record with a `Count`.

- Can belong to one `ProductGroup` (via `GroupId`)
- Can participate in one or more `Variation` containers
- Can be a component of a `Bundle`

### Unit

A serialized item where each physical unit is individually tracked. Each physical unit is an `UnitInventoryItem` stored in a specific warehouse node and carries a unique `Sku`.

- Can belong to one `ProductGroup` (via `GroupId`)
- Can participate in one or more `Variation` containers
- Can be a component of a `Bundle`

### ProductGroup

A virtual grouping of related Standard or Unit items (e.g. a clothing item with sizes/colors as children). The group itself is never stored in inventory.

**Naming rule:** child items that belong to a ProductGroup display their full name as `Group.Name + " " + Item.Name` (exposed as `FullName` on the DTO).

**Child item management:** children are created and edited only via the ProductGroup's `children` field in Create/Update — not through the general catalog endpoints directly. The list is a full-replace sync — `id: null` creates a child, an `id` updates one, and a child missing from the list is deleted. See [backend-patterns.md](backend-patterns.md#updating-related-entity-lists-with-ilistupdater).

**Inheritable fields:** `Description` and `Notes`. If a child has a `null` value for one of these fields, the effective value is resolved from the parent group. See [Inheritable Fields](#inheritable-fields).

### Variation

A virtual container that groups multiple Standard, Unit, or Bundle items as interchangeable variations (e.g. "iPhone 15 sizes") — **not** another Variation directly. A single item can belong to multiple Variation containers (many-to-many). The Variation itself carries no inventory. See [Bundle/Variation nesting](#bundlevariation-nesting) below.

### Bundle

A configurable kit composed of any combination of Standard, Unit, ProductGroup, or Variation components — **not** another Bundle directly — each with a quantity. A Bundle can be modified over time.

Components are stored in `BundleComponent` records linked to the Bundle's CatalogItem.

#### Bundle/Variation nesting

Bundle and Variation can nest each other to arbitrary depth: a Bundle component may be a Variation, and a Variation member may be a Bundle (`Bundle → Variation → Bundle → ...`), as long as the result is acyclic. The only two combinations that are disallowed are a Bundle directly containing another Bundle, and a Variation directly containing another Variation.

Both Bundle saves and Variation saves run a standalone circular-dependency check (`ICatalogService.EnsureNoCycleAsync`), which walks this Bundle↔Variation edge graph with a recursion-stack DFS. A cycle is rejected with `422 catalogItemCircularDependency`.

---

## FullName Rule

**Always use `FullName` (not `Name`) when displaying a CatalogItem to the user.**

`FullName` is a computed field:
- For items with a parent group: `Group.Name + " " + Item.Name`
- For standalone items: `Item.Name`

Using `Name` alone omits the group prefix and produces incomplete, ambiguous labels (e.g. "Red" instead of "T-Shirt Red").

**Backend:** `FullName` is a `[Projectable]` property on `CatalogItem` — it works in both in-memory and EF Core `ProjectTo` queries. All mapper mappings that produce display names (e.g. `BundleComponentDto.ComponentName`, `AppEntity.Name` for catalog items) must use `s.Component.FullName` / `ci.FullName`.

**Frontend:** All DTOs expose `fullName`. Use `item.fullName` in tables, selects, chips, drawers, and any other display surface. The raw `item.name` field is used only in form inputs where the user edits just the item's own name (without the group prefix).

---

## Fields by Type

| Field | Standard | Unit | ProductGroup | Variation | Bundle |
|-------|----------|------|--------------|-----------|--------|
| `Name` | ✓ | ✓ | ✓ | ✓ | ✓ |
| `FullName` (computed) | ✓ | ✓ | ✓ | ✓ | ✓ |
| `Article` | ✓ | ✓ | ✓ | ✓ | ✓ |
| `Barcode` | opt | opt | opt | — | — |
| `Description` ¹ | opt | opt | opt | opt | opt |
| `Notes` ¹ | opt | opt | opt | opt | opt |
| `IsArchived` | ✓ | ✓ | ✓ | ✓ | ✓ |
| `Tags` | opt | opt | opt | opt | opt |
| `GroupId` | opt | opt | — | — | — |
| `BundleComponents` | — | — | — | — | ✓ |
| `VariationMemberships` | opt | opt | — | — | opt |
| `VariationMembers` | — | — | — | ✓ | — |
| `GroupChildren` | — | — | ✓ | — | — |

¹ Inheritable — see [Inheritable Fields](#inheritable-fields) below.

---

## Inventory Storage

### Standard → ItemsGroup

```
StoragePlaceNode (1) ──> (many) StoragePlaceNodeItemsGroup
                                  ├── CatalogItemId  → CatalogItem (type=Standard)
                                  └── Count
```

### Unit → UnitInventoryItem

```
StoragePlaceNode (1) ──> (many) UnitInventoryItem : InventoryItem
                                  ├── CatalogItemId  → CatalogItem (type=Unit)
                                  └── Sku
```

`InventoryItem` uses TPH — all subtypes share the `InventoryItems` table with a `Type` discriminator column.

Every stock change of either kind is journalled as a `StockMovement` in the same transaction. For a unit item the
row also carries `UnitInventoryItemId` and a denormalized `UnitInventoryNumber`; standard quantity movements leave
both `null`. The id is an audit reference like the other location columns — deleting the item nulls the link instead
of erasing the movement — while the copied number stays, so the history of a piece that no longer exists is still
readable and searchable by its number.

The document that caused the change is passed to `InventoryService` as an optional `StockMovementContext`
(`ReceiptId`, extended as more document types get linked) and lands on the row as a nullable FK. Receipt placement
endpoints supply the receipt they act on; movements made outside any document leave it `null`. Like the other
references it is `ON DELETE SET NULL`, so deleting a receipt keeps its movements.

---

## Tags

A CatalogItem can have zero or more tags. Tags are a flat list with no hierarchy. The relationship is many-to-many with no primary/default concept — all tags are equal.

**ProductGroup tag copying:** whenever a child item is created or updated via a ProductGroup's `children` list, the group's current tags are automatically merged into the child's tag set (union of the child's own `tags` from the request and the group's `tags`). This means changes to the group's tags propagate to all children on the next group update. Tags added to a child individually are preserved; however, tags removed from the group are not automatically removed from children.

---

## Inheritable Fields

`Description` and `Notes` support inheritance from a parent `ProductGroup`. The domain stores the raw value; the effective resolved value is computed by a `[Projectable]` property and surfaced in the DTO.

| Field | Domain type | Effective resolution |
|-------|-------------|----------------------|
| `Description` | `string?` | `Description ?? Group?.Description` |
| `Notes` | `string?` | `Notes ?? Group?.Notes` |

**Rules:**
- A `null` value on a child item means "inherit from parent". The DTO always exposes the resolved effective value.
- A non-null value always takes precedence over the parent's value — a child can explicitly override even if the parent has a value.
- Inheritance is one level deep: children inherit from their immediate `ProductGroup` parent only.
- Non-group items (no `GroupId`) have no parent to inherit from — effective value equals the stored value.
- `IsArchived` is **not** inheritable via the null-resolution mechanism, but is **propagated** from the group to all children on every Create/Update of the ProductGroup — children always mirror the group's `IsArchived` value.

The mechanism (`Effective*` `[Projectable]` properties plus the mapper wiring that swaps them in) is described in [backend-patterns.md](backend-patterns.md#inheritable-fields-with-projectable).

---

## Images

An item has one main image (`mainImageFileId`) and a gallery (`images: [{ id, fileId, order }]`, a link list on
the same full-replace contract — `id: null` creates the link, an omitted link is removed). Dropping a link never
deletes the file; the [GC](data-files-specification.md) takes it later.

`CatalogItemDto.mainImage` is the **effective** image — the item's own, otherwise the parent group's, exactly the
way `Description` and `Notes` resolve. `mainImageFileId` stays `null` while `mainImage` is populated when the
image is inherited, and that pair is the only way the UI distinguishes "own" from "inherited". An edit form must
bind `mainImageFileId`, never `mainImage`, or saving would silently copy the group's image onto the child.

Unlike `Description` and `Notes`, the effective main image is **not** a `[Projectable]`. Every `[Projectable]`
here returns a `string` or a `bool`; one returning a navigation would coalesce two entity references, and whether
EF folds member access over that into `CASE WHEN` is untested. Worse, the same expression run in memory by
`mapper.Map` silently yields null unless both navigations are `Include`d — a failure invisible until a screenshot
looks wrong. Instead the mapper holds one shared expression that builds the DTO on **both** branches: branches
returning a non-entity type translate unambiguously and stay correct in memory.

The gallery is **never** inherited. `s.Images.Any() ? s.Images : s.Group.Images` is a conditional over a
collection, which EF cannot translate, and the workaround breaks on recursion into children. It is also worse
semantically: "child has no images, so show all of the group's" makes "this variant deliberately has only a main
photo" unsayable.

`POST /api/catalog` accepts `mainImageFileId` only; `PUT` and each ProductGroup child accept both fields.
A reference to a file that does not exist is `422 dataFileNotFound`.

---

## Listing and Filtering

Two read endpoints with a deliberate difference:

- `GET /api/catalog` — the paginated management list. It excludes ProductGroup children, which are edited
  through their group rather than on their own.
- `GET /api/catalog/for-select` — the flat feed behind select/autocomplete controls. It **does** include group
  children, because a picker has to offer the concrete item that goes into a receipt or an order.

`tagIds` filters with OR semantics — an item matches if it carries any of the listed tags. Only the item's own
tags are considered; a group's tags are not walked down at query time, because the [tag copying](#tags) rule has
already merged them into each child's own set.

Archived items are always sorted last regardless of `sortBy`.

---

## Type Immutability

A `CatalogItem`'s `Type` is set at creation and **cannot be changed**. This is enforced at two levels:

1. **`Update` endpoint** — `UpdateCatalogItemRequest` has no `type` field; the stored type is never touched.
2. **`SyncGroupChildren`** — when updating existing children of a ProductGroup, the request's `type` value is validated against the stored type. A mismatch returns `422 catalogItemIsImmutable`.

The rationale: the type determines which related tables and navigation properties are meaningful (e.g. `BundleComponents` only exist for `Bundle`, `InventoryItems` only point to `Unit`). Allowing type changes in-place would silently orphan or corrupt those relationships.
