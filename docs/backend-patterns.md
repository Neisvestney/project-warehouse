# Backend Patterns

Recurring implementation patterns used in the server project.

---

## Search with `WhereMatchesSearch` + `[Projectable]`

### Pattern

Full-text search across multiple entity fields is done via two pieces:

1. **`[Projectable]` `SearchString` on the domain entity** — concatenates all searchable fields into one string. EF Projectables expands this property inline during SQL translation, so no extra joins or subqueries are generated.
2. **`WhereMatchesSearch(e => e.SearchString, searchString)`** — applies a case-insensitive `ILIKE` filter per token (AND semantics across tokens, OR across fields happens implicitly via the concatenated string).

### Example

**Domain entity** (`CatalogItem.cs`):
```csharp
[Projectable]
public string SearchString =>
    (Name ?? "") + " " + (Article ?? "");
```

**Controller** (`CatalogController.cs`):
```csharp
db.CatalogItems
    .WhereMatchesSearch(c => c.SearchString, searchString)
    ...
```

### Searching across related entities

When the searchable fields span a navigation property, put `SearchString` on the related entity and navigate to it in the expression:

**Domain entity** (`CatalogItemWithCharacteristic.cs`):
```csharp
[Projectable]
public string SearchString =>
    (CatalogItem.Name ?? "") + " " +
    (CatalogItem.Article ?? "") + " " +
    (CatalogItem.Barcode ?? "") + " " +
    (Barcode ?? "") + " " +
    (Characteristic ?? "");
```

**Controller** (`WarehousesController.cs`):
```csharp
db.StoragePlacesNodesItemsGroups
    .WhereMatchesSearch(g => g.CatalogItemWithCharacteristic.SearchString, searchString)
    ...
```

EF Projectables intercepts the `SearchString` member access during LINQ-to-SQL translation and expands it inline — the navigation is transparent to EF Core.

### Rules

- Always use `?? ""` on nullable string fields inside `SearchString` to avoid null propagation in SQL.
- Non-nullable string fields don't strictly need `?? ""`, but it's kept for consistency.
- `WhereMatchesSearch` with a `null` or whitespace `searchString` is a no-op — no filter is applied.
- Token splitting is space-based; each token must appear somewhere in the concatenated string (AND across tokens).
