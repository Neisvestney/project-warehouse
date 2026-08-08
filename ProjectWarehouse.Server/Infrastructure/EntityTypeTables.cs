using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using ProjectWarehouse.Server.Domain;

namespace ProjectWarehouse.Server.Infrastructure;

/// <summary>
/// Groups database tables under the <see cref="AppEntityType"/> they belong to, for the storage
/// statistics page. Table names come from the EF model rather than from string literals, so a
/// renamed table follows automatically; only the CLR type has to be listed here.
/// </summary>
/// <remarks>
/// Tables that belong to no user-facing entity — uploaded files, Identity's claim and token tables,
/// the migrations history — stay <see cref="AppEntityType.Unknown"/> and show up under "Прочее".
/// </remarks>
public static class EntityTypeTables
{
    private static readonly Dictionary<Type, AppEntityType> Owners = new()
    {
        [typeof(ApplicationUser)] = AppEntityType.User,
        [typeof(UserPermission)] = AppEntityType.User,
        [typeof(RefreshToken)] = AppEntityType.User,

        [typeof(ApplicationRole)] = AppEntityType.Roles,
        [typeof(ApplicationUserRole)] = AppEntityType.Roles,
        [typeof(RolePermission)] = AppEntityType.Roles,

        [typeof(Warehouse)] = AppEntityType.Warehouse,
        [typeof(StoragePlace)] = AppEntityType.Warehouse,

        [typeof(StoragePlaceNode)] = AppEntityType.StoragePlaceNode,
        [typeof(StoragePlaceNodeItemsGroup)] = AppEntityType.StoragePlaceNode,

        [typeof(CatalogItem)] = AppEntityType.CatalogItem,
        [typeof(CatalogItemTag)] = AppEntityType.CatalogItem,
        [typeof(CatalogItemImage)] = AppEntityType.CatalogItem,
        [typeof(CatalogItemVariationMember)] = AppEntityType.CatalogItem,
        [typeof(BundleComponent)] = AppEntityType.CatalogItem,
        // TPH root; UnitInventoryItem shares the table and resolves through the base type
        [typeof(InventoryItem)] = AppEntityType.InventoryItem,

        [typeof(ChangeLogEntry)] = AppEntityType.ChangeLog,

        [typeof(StockMovement)] = AppEntityType.StockMovement,

        [typeof(Receipt)] = AppEntityType.Receipt,
        [typeof(ReceiptItem)] = AppEntityType.Receipt,
        [typeof(ReceiptItemPlacement)] = AppEntityType.Receipt,

        [typeof(Writeoff)] = AppEntityType.Writeoff,
        [typeof(WriteoffItem)] = AppEntityType.Writeoff,

        [typeof(Order)] = AppEntityType.Order,
        [typeof(MarketplaceOrder)] = AppEntityType.Order,
        [typeof(OrderMarketplaceItem)] = AppEntityType.Order,
        [typeof(OrderBox)] = AppEntityType.Order,
        [typeof(OrderBoxComponent)] = AppEntityType.Order,
        [typeof(AssemblyTask)] = AppEntityType.Order,
        [typeof(AssemblyTaskBox)] = AppEntityType.Order,
        [typeof(AssemblyTaskBoxComponent)] = AppEntityType.Order,
        [typeof(AssemblyFulfillment)] = AppEntityType.Order,
        [typeof(AssemblyFulfillmentBundleComponent)] = AppEntityType.Order,

        [typeof(MarketplaceAccount)] = AppEntityType.MarketplaceAccount,
        [typeof(MarketplaceWarehouse)] = AppEntityType.MarketplaceAccount,
        [typeof(MarketplaceSyncRun)] = AppEntityType.MarketplaceAccount,

        [typeof(MarketplaceCard)] = AppEntityType.MarketplaceCard,
    };

    private static readonly ConcurrentDictionary<IModel, IReadOnlyDictionary<string, AppEntityType>> Cache = new();

    /// <summary>Table name to owner. Tables absent from the result are <see cref="AppEntityType.Unknown"/>.</summary>
    public static IReadOnlyDictionary<string, AppEntityType> Resolve(IModel model) =>
        Cache.GetOrAdd(model, static m =>
        {
            var result = new Dictionary<string, AppEntityType>(StringComparer.Ordinal);

            foreach (var entityType in m.GetEntityTypes())
            {
                var table = entityType.GetTableName();
                if (table is null) continue;

                var owner = ResolveOwner(entityType.ClrType);
                if (owner == AppEntityType.Unknown && IsImplicitJoinEntity(entityType))
                    owner = ResolveJoinOwner(entityType);

                // TPH puts several types on one table; a mapped one wins over an unmapped sibling
                if (owner != AppEntityType.Unknown || !result.ContainsKey(table))
                    result[table] = owner;
            }

            return result;
        });

    private static AppEntityType ResolveOwner(Type? clrType)
    {
        for (var type = clrType; type is not null; type = type.BaseType)
            if (Owners.TryGetValue(type, out var owner))
                return owner;

        return AppEntityType.Unknown;
    }

    // a many-to-many EF generates itself has no class to list above
    private static bool IsImplicitJoinEntity(IReadOnlyEntityType entityType) =>
        entityType.ClrType == typeof(Dictionary<string, object>);

    /// <summary>
    /// A join table belongs to its sides. Both ends must agree — CatalogItems×CatalogItemTags is
    /// clearly the catalog, but Users×Warehouses belongs to neither and stays unknown.
    /// </summary>
    private static AppEntityType ResolveJoinOwner(IReadOnlyEntityType entityType)
    {
        var owners = entityType.GetForeignKeys()
            .Select(fk => ResolveOwner(fk.PrincipalEntityType.ClrType))
            .Distinct()
            .ToList();

        return owners is [var single] && single != AppEntityType.Unknown ? single : AppEntityType.Unknown;
    }
}
