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
| Assembled Bundle | `AssembledBundle` | No | `AssembledBundleInventoryItem` |

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

**Child item management:** children are created and edited only via the ProductGroup's `children` field in Create/Update — not through the general catalog endpoints directly.

**Inheritable fields:** `Description` and `Notes`. If a child has a `null` value for one of these fields, the effective value is resolved from the parent group. See [Inheritable Fields](#inheritable-fields).

### Variation

A virtual container that groups multiple Standard or Unit items as interchangeable variations (e.g. "iPhone 15 sizes"). A single item can belong to multiple Variation containers (many-to-many). The Variation itself carries no inventory.

### Bundle

A configurable kit composed of any combination of Standard, Unit, ProductGroup, or Variation components, each with a quantity. A Bundle can be modified over time.

Components are stored in `BundleComponent` records linked to the Bundle's CatalogItem.

### AssembledBundle

An immutable snapshot of a specific Bundle assembly. Created when a Bundle is physically assembled (e.g. during order picking). If an identical configuration already exists in the catalog, the existing AssembledBundle CatalogItem is reused.

- References its source Bundle via `SourceBundleId`
- Cannot be modified after creation
- Physically stored via `AssembledBundleInventoryItem`
- If the Bundle contains Unit items, the `AssembledBundleInventoryItem` records the specific `UnitInventoryItem` instances (by SKU) used in that assembly
- If the Bundle contains Standard items, the component is recorded by `CatalogItem` reference + quantity

---

## Fields by Type

| Field | Standard | Unit | ProductGroup | Variation | Bundle | AssembledBundle |
|-------|----------|------|--------------|-----------|--------|-----------------|
| `Name` | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| `Article` | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| `Barcode` | opt | opt | opt | — | — | — |
| `Description` ¹ | opt | opt | opt | opt | opt | opt |
| `Notes` ¹ | opt | opt | opt | opt | opt | opt |
| `IsArchived` | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| `Tags` | opt | opt | opt | opt | opt | opt |
| `GroupId` | opt | opt | — | — | — | — |
| `SourceBundleId` | — | — | — | — | — | ✓ |
| `BundleComponents` | — | — | — | — | ✓ | — |
| `AssembledComponents` | — | — | — | — | — | ✓ |
| `VariationMemberships` | opt | opt | — | — | — | — |
| `VariationMembers` | — | — | — | ✓ | — | — |
| `GroupChildren` | — | — | ✓ | — | — | — |

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

### AssembledBundle → AssembledBundleInventoryItem

```
StoragePlaceNode (1) ──> (many) AssembledBundleInventoryItem : InventoryItem
                                  ├── CatalogItemId  → CatalogItem (type=AssembledBundle)
                                  └── Components[]
                                        ├── UnitInventoryItemId  (if Unit component)
                                        └── CatalogItemId + Quantity  (if Standard component)
```

`InventoryItem` uses TPH — all subtypes share the `InventoryItems` table with a `Type` discriminator column.

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

**Implementation:**

On `CatalogItem`:
```csharp
[Projectable]
public string? EffectiveDescription => Description ?? (Group != null ? Group.Description : null);

[Projectable]
public string? EffectiveNotes => Notes ?? (Group != null ? Group.Notes : null);
```

In `AppMapperProfile`:
```csharp
CreateMap<CatalogItem, CatalogItemDto>()
    .ForMember(d => d.Description, opt => opt.MapFrom(s => s.EffectiveDescription))
    .ForMember(d => d.Notes,       opt => opt.MapFrom(s => s.EffectiveNotes))
    ...
```

The `[Projectable]` attribute (EntityFrameworkCore.Projectables) allows these properties to be used both in-memory (when entities are loaded via `Include`) and in EF Core `ProjectTo` queries (translated to SQL with a LEFT JOIN on the parent row).

---

## Type Immutability

A `CatalogItem`'s `Type` is set at creation and **cannot be changed**. This is enforced at two levels:

1. **`Update` endpoint** — `UpdateCatalogItemRequest` has no `type` field; the stored type is never touched.
2. **`SyncGroupChildren`** — when updating existing children of a ProductGroup, the request's `type` value is validated against the stored type. A mismatch returns `422 catalogItemIsImmutable`.

The rationale: the type determines which related tables and navigation properties are meaningful (e.g. `BundleComponents` only exist for `Bundle`, `InventoryItems` only point to `Unit`/`AssembledBundle`). Allowing type changes in-place would silently orphan or corrupt those relationships.
