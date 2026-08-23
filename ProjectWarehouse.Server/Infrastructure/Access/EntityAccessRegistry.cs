using ProjectWarehouse.Server.Data;
using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Infrastructure.Access.Rules;

namespace ProjectWarehouse.Server.Infrastructure.Access;

/// <summary>
/// The whole "entity type → permission" map, in one place. An <see cref="AppEntityType"/> with no rule
/// here is inaccessible: realtime cannot subscribe to it and no filter will return its rows.
/// </summary>
public class EntityAccessRegistry
{
    private readonly Dictionary<AppEntityType, IEntityAccessRule> _byEntityType;
    private readonly Dictionary<Type, IEntityAccessRule> _byClrType;

    public EntityAccessRegistry(ApplicationDbContext db, AccessScope scope)
    {
        IEntityAccessRule[] rules =
        [
            new WarehouseScopedRule<Warehouse>(db, scope, AppEntityType.Warehouse,
                viewAll: [Permissions.Warehouses.View],
                viewAssigned: [Permissions.Warehouses.ViewAssigned],
                editAll: [Permissions.Warehouses.Edit],
                editAssigned: [Permissions.Warehouses.EditAssigned],
                warehouse: w => w.Id,
                ErrorCode.WarehouseNotAssigned,
                "You are not assigned to this warehouse."),

            new WarehouseScopedRule<StoragePlaceNode>(db, scope, AppEntityType.StoragePlaceNode,
                viewAll: [Permissions.Warehouses.View],
                viewAssigned: [Permissions.Warehouses.ViewAssigned],
                editAll: [Permissions.Warehouses.Edit],
                editAssigned: [Permissions.Warehouses.EditAssigned],
                warehouse: n => n.RootStoragePlace.WarehouseId,
                ErrorCode.StoragePlaceNotAssignedToWarehouse,
                "You are not assigned to the warehouse of this storage place."),

            new ReceiptAccessRule(db, scope),

            new WarehouseScopedRule<Writeoff>(db, scope, AppEntityType.Writeoff,
                viewAll: [Permissions.Writeoffs.View],
                viewAssigned: [Permissions.Writeoffs.ViewAssigned],
                editAll: [Permissions.Writeoffs.Edit],
                editAssigned: [Permissions.Writeoffs.EditAssigned],
                warehouse: w => w.WarehouseId,
                ErrorCode.WriteoffNotAssignedToWarehouse,
                "You are not assigned to the warehouse of this write-off."),

            new WarehouseScopedRule<Stocktake>(db, scope, AppEntityType.Stocktake,
                viewAll: [Permissions.Stocktakes.View],
                viewAssigned: [Permissions.Stocktakes.ViewAssigned],
                editAll: [Permissions.Stocktakes.Edit],
                editAssigned: [Permissions.Stocktakes.EditAssigned],
                warehouse: s => s.WarehouseId,
                ErrorCode.StocktakeNotAssignedToWarehouse,
                "You are not assigned to the warehouse of this stocktake."),

            // AssembleAssigned views orders exactly like ViewAssigned does — an assembler who cannot read
            // the order cannot work on it. It grants no edit access: assembly runs through its own endpoints.
            new WarehouseScopedRule<Order>(db, scope, AppEntityType.Order,
                viewAll: [Permissions.Orders.View],
                viewAssigned: [Permissions.Orders.ViewAssigned, Permissions.Orders.AssembleAssigned],
                editAll: [Permissions.Orders.Edit],
                editAssigned: [Permissions.Orders.EditAssigned],
                warehouse: o => o.WarehouseId,
                ErrorCode.OrderNotAssignedToWarehouse,
                "You are not assigned to the warehouse of this order."),

            new StockMovementAccessRule(db, scope),

            new SimpleAccessRule<CatalogItem>(db, AppEntityType.CatalogItem,
                Permissions.Catalog.View, Permissions.Catalog.Edit),

            new SimpleAccessRule<ApplicationUser>(db, AppEntityType.User,
                Permissions.Users.View, Permissions.Users.EditProfile),

            new SimpleAccessRule<ApplicationRole>(db, AppEntityType.Roles,
                Permissions.Roles.View, Permissions.Roles.Edit),

            new SimpleAccessRule<MarketplaceAccount>(db, AppEntityType.MarketplaceAccount,
                Permissions.Integrations.View, Permissions.Integrations.Edit),

            new SimpleAccessRule<MarketplaceCard>(db, AppEntityType.MarketplaceCard,
                Permissions.Integrations.View, Permissions.Integrations.Edit),

            // Registered under the plural type: the lock and the change event address the rule set as a
            // whole, and mapping is the right that editing a rule actually needs.
            new SimpleAccessRule<MarketplaceAutoMapRule>(db, AppEntityType.MarketplaceAutoMapRules,
                Permissions.Integrations.View, Permissions.Integrations.Map),
        ];

        _byEntityType = rules.ToDictionary(r => r.EntityType);
        _byClrType = rules.ToDictionary(r => r.ClrType);
    }

    public IEntityAccessRule? Find(AppEntityType entityType) =>
        _byEntityType.GetValueOrDefault(entityType);

    public EntityAccessRule<T> For<T>() where T : class =>
        _byClrType.TryGetValue(typeof(T), out var rule)
            ? (EntityAccessRule<T>)rule
            : throw new InvalidOperationException($"No access rule registered for {typeof(T).Name}.");
}
