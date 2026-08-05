using System.Text.Json;
using AutoMapper;
using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Models.ChangeLog;
using ProjectWarehouse.Server.Models;
using ProjectWarehouse.Server.Models.Catalog;
using ProjectWarehouse.Server.Models.Events;
using ProjectWarehouse.Server.Models.Integrations;
using ProjectWarehouse.Server.Models.Inventory;
using ProjectWarehouse.Server.Models.Receipts;
using ProjectWarehouse.Server.Models.Roles;
using ProjectWarehouse.Server.Models.Writeoffs;
using ProjectWarehouse.Server.Models.Users;
using ProjectWarehouse.Server.Models.Warehouses;
using ProjectWarehouse.Server.Models.Orders;

namespace ProjectWarehouse.Server.Infrastructure;

public class AppMapperProfile : Profile
{
    public AppMapperProfile()
    {
        CreateMap<ApplicationRole, RoleDto>();
        CreateMap<ApplicationRole, RoleWithPermissionsDto>()
            .ForMember(d => d.Permissions, opt => opt.MapFrom(s => s.RolePermissions.Select(rp => rp.Permission)));
        CreateMap<ApplicationUser, UserDetailDto>()
            .ForMember(d => d.Username, opt => opt.MapFrom(s => s.UserName))
            .ForMember(d => d.Roles, opt => opt.MapFrom(s => s.UserRoles.Select(ur => ur.Role)))
            .ForMember(d => d.DirectPermissions, opt => opt.MapFrom(s => s.UserPermissions.Select(up => up.Permission)))
            .ForMember(d => d.AssignedWarehouses, opt => opt.MapFrom(s => s.AssignedWarehouses));
        CreateMap<ApplicationUser, UserDto>()
            .ForMember(d => d.Username, opt => opt.MapFrom(s => s.UserName));

        CreateMap<CatalogItemTag, CatalogItemTagDto>();
        CreateMap<BundleComponent, BundleComponentDto>()
            .ForMember(d => d.ComponentName, opt => opt.MapFrom(s => s.Component.FullName))
            .ForMember(d => d.ComponentType, opt => opt.MapFrom(s => s.Component.Type));

        CreateMap<CatalogItem, CatalogItemDto>()
            .ForMember(d => d.GroupName, opt => opt.MapFrom(s => s.Group != null ? s.Group.Name : null))
            .ForMember(d => d.Description, opt => opt.MapFrom(s => s.EffectiveDescription))
            .ForMember(d => d.Notes, opt => opt.MapFrom(s => s.EffectiveNotes))
            .ForMember(d => d.Components, opt => opt.MapFrom(s => s.BundleComponents))
            .ForMember(d => d.VariationIds, opt => opt.MapFrom(s => s.VariationMemberships.Select(m => m.VariationId).ToList()))
            .ForMember(d => d.MemberIds, opt => opt.MapFrom(s => s.VariationMembers.Select(m => m.ItemId).ToList()))
            .ForMember(d => d.Children, opt => opt.MapFrom(s => s.GroupChildren));
        CreateMap<CatalogItem, CatalogItemSummaryDto>();
        CreateMap<CatalogItem, CatalogItemSelectDto>();
        CreateMap<CatalogItem, NodeCatalogItemDto>();

        CreateMap<WarehouseLayoutElement, WarehouseLayoutElementDto>();
        CreateMap<StoragePlace, StoragePlaceDto>();
        CreateMap<StoragePlaceNode, StoragePlaceNodeDto>();
        CreateMap<StoragePlaceNodeItemsGroup, ItemsGroupDto>();
        CreateMap<StoragePlaceNode, StoragePlaceNodeDetailsDto>()
            .ForMember(d => d.Name, opt => opt.MapFrom(s => new[] { s.Name }))
            .ForMember(d => d.StoragePlaceId, opt => opt.MapFrom(s => s.RootStoragePlaceId))
            .ForMember(d => d.ItemsGroups, opt => opt.MapFrom(s => s.ItemsGroups))
            .ForMember(d => d.UnitItemsCount,
                opt => opt.MapFrom(s => s.InventoryItems.OfType<UnitInventoryItem>().Count()));
        CreateMap<Warehouse, WarehouseDto>()
            .ForMember(x => x.MarketplaceAccounts, opt => opt.MapFrom(w =>
                w.MarketplaceWarehouses.Select(mw => mw.MarketplaceAccount).Distinct()
            ));
        CreateMap<Warehouse, WarehouseSummaryDto>()
            .ForMember(d => d.StoragePlaceCount, opt => opt.MapFrom(s => s.StoragePlaces.Count));

        CreateMap<UnitInventoryItem, UnitInventoryItemDto>()
            .ForMember(d => d.NodeId, opt => opt.MapFrom(s => s.StoragePlaceNodeId))
            .ForMember(d => d.NodeName, opt => opt.MapFrom(s => s.StoragePlaceNode!.Name))
            .ForMember(d => d.StoragePlaceId, opt => opt.MapFrom(s => s.StoragePlaceNode!.RootStoragePlaceId))
            .ForMember(d => d.StoragePlaceName, opt => opt.MapFrom(s => s.StoragePlaceNode!.RootStoragePlace.Name))
            .ForMember(d => d.WarehouseId, opt => opt.MapFrom(s => s.StoragePlaceNode!.RootStoragePlace.WarehouseId))
            .ForMember(d => d.WarehouseName, opt => opt.MapFrom(s => s.StoragePlaceNode!.RootStoragePlace.Warehouse.Name));

        CreateMap<Warehouse, AppEntity>()
            .ForMember(x => x.Type, opt => opt.MapFrom(_ => AppEntityType.Warehouse))
            .ForMember(x => x.AdditionalFields, opt => opt.MapFrom(r => new Dictionary<string, object>
            {
                { "totalItemsCount", r.TotalItemsCount },
            }));

        CreateMap<Receipt, AppEntity>()
            .ForMember(x => x.Type, opt => opt.MapFrom(_ => AppEntityType.Receipt))
            .ForMember(x => x.AdditionalFields, opt => opt.MapFrom(r => new Dictionary<string, object>
            {
                { "number", r.Number },
                { "status", r.Status },
            }));
        
        CreateMap<CatalogItem, AppEntity>()
            .ForMember(x => x.Type, opt => opt.MapFrom(_ => AppEntityType.CatalogItem))
            .ForMember(x => x.Name, opt => opt.MapFrom(ci => ci.FullName))
            .ForMember(x => x.AdditionalFields, opt => opt.MapFrom(r => new Dictionary<string, object>
            {
                { "type", r.Type },
                { "article", r.Article },
            }));
        
        CreateMap<ApplicationUser, AppEntity>()
            .ForMember(x => x.Type, opt => opt.MapFrom(_ => AppEntityType.User))
            .ForMember(x => x.Name, opt => opt.MapFrom(ci => ci.FullName))
            .ForMember(x => x.AdditionalFields, opt => opt.MapFrom(r => new Dictionary<string, object>
            {
                { "username", r.UserName ?? "" },
                { "email", r.Email ?? "" },
            }));

        CreateMap<Warehouse, AppEntityWithSearchString>()
            .ForMember(x => x.AppEntity, opt => opt.MapFrom(x => x));
        
        CreateMap<Receipt, AppEntityWithSearchString>()
            .ForMember(x => x.AppEntity, opt => opt.MapFrom(x => x));
        
        CreateMap<CatalogItem, AppEntityWithSearchString>()
            .ForMember(x => x.AppEntity, opt => opt.MapFrom(x => x));
        
        CreateMap<ApplicationUser, AppEntityWithSearchString>()
            .ForMember(x => x.AppEntity, opt => opt.MapFrom(x => x));
        
        CreateMap<Receipt, EventDto>()
            .ForMember(x => x.AppEntity, opt => opt.MapFrom(x => x))
            .ForMember(x => x.StartDate, opt => opt.MapFrom(x => x.PlannedDeliveryDate))
            .ForMember(x => x.EndDate, opt => opt.MapFrom(x => x.PlannedDeliveryDate));
        
        CreateMap<Receipt, ReceiptDto>()
            .ForMember(d => d.WarehouseName, opt => opt.MapFrom(s => s.Warehouse.Name))
            .ForMember(d => d.TotalPlannedCount, opt => opt.MapFrom(s => s.Items.Sum(i => i.PlannedCount)))
            .ForMember(d => d.TotalReceivedCount, opt => opt.MapFrom(s => s.Items.Sum(i => i.ReceivedCount ?? 0)));
        CreateMap<Receipt, ReceiptSummaryDto>()
            .ForMember(d => d.WarehouseName, opt => opt.MapFrom(s => s.Warehouse.Name))
            .ForMember(d => d.ItemsCount, opt => opt.MapFrom(s => s.Items.Count))
            .ForMember(d => d.TotalPlannedCount, opt => opt.MapFrom(s => s.Items.Sum(i => i.PlannedCount)))
            .ForMember(d => d.TotalReceivedCount, opt => opt.MapFrom(s => s.Items.Sum(i => i.ReceivedCount ?? 0)));
        CreateMap<ReceiptItem, ReceiptItemDto>();
        CreateMap<ReceiptItemPlacement, ReceiptItemPlacementDto>()
            .ForMember(d => d.NodePath, opt => opt.MapFrom<NodePathResolver>())
            .ForMember(d => d.InventoryNumber,
                opt => opt.MapFrom(s => s.UnitInventoryItem != null ? s.UnitInventoryItem.InventoryNumber : null));

        CreateMap<Writeoff, WriteoffSummaryDto>()
            .ForMember(d => d.WarehouseName, opt => opt.MapFrom(s => s.Warehouse.Name))
            .ForMember(d => d.ItemsCount, opt => opt.MapFrom(s => s.Items.Count));
        CreateMap<Writeoff, WriteoffDto>()
            .ForMember(d => d.WarehouseName, opt => opt.MapFrom(s => s.Warehouse.Name));
        CreateMap<WriteoffItem, WriteoffItemDto>()
            .ForMember(d => d.SourceNodePath, opt => opt.MapFrom<WriteoffItemNodePathResolver>())
            .ForMember(d => d.InventoryNumber,
                opt => opt.MapFrom(s => s.UnitInventoryItem != null ? s.UnitInventoryItem.InventoryNumber : null))
            .ForMember(d => d.CatalogItemName, opt => opt.MapFrom<WriteoffItemCatalogNameResolver>());

        CreateMap<Writeoff, AppEntity>()
            .ForMember(x => x.Type, opt => opt.MapFrom(_ => AppEntityType.Writeoff))
            .ForMember(x => x.AdditionalFields, opt => opt.MapFrom(r => new Dictionary<string, object>
            {
                { "number", r.Number },
                { "status", r.Status },
            }));

        CreateMap<Writeoff, AppEntityWithSearchString>()
            .ForMember(x => x.AppEntity, opt => opt.MapFrom(x => x));

        CreateMap<Order, OrderSummaryDto>()
            .ForMember(d => d.WarehouseName, opt => opt.MapFrom(s => s.Warehouse.Name))
            .ForMember(d => d.CreatedByName, opt => opt.MapFrom(s => s.CreatedBy != null ? s.CreatedBy.FullName : null))
            .ForMember(d => d.BoxCount, opt => opt.MapFrom(s => s.Boxes.Count))
            .ForMember(d => d.ComponentCount, opt => opt.MapFrom(s => s.Boxes.SelectMany(b => b.Components).Sum(c => c.Quantity)));

        CreateMap<Order, OrderDetailsDto>()
            .ForMember(d => d.WarehouseName, opt => opt.MapFrom(s => s.Warehouse.Name))
            .ForMember(d => d.CreatedByName, opt => opt.MapFrom(s => s.CreatedBy != null ? s.CreatedBy.FullName : null));

        CreateMap<OrderBox, OrderBoxDto>();

        CreateMap<OrderBoxComponent, OrderBoxComponentDto>()
            .ForMember(d => d.CatalogItemName, opt => opt.MapFrom(s => s.CatalogItem.FullName))
            .ForMember(d => d.CatalogItemType, opt => opt.MapFrom(s => s.CatalogItem.Type));

        CreateMap<AssemblyTask, AssemblyTaskDto>()
            .ForMember(d => d.AssignedToName, opt => opt.MapFrom(s => s.AssignedTo != null ? s.AssignedTo.FullName : null));

        CreateMap<AssemblyTaskBox, AssemblyTaskBoxDto>()
            .ForMember(d => d.OrderBoxLabel, opt => opt.MapFrom(s => s.OrderBox.Label));

        CreateMap<AssemblyTaskBoxComponent, AssemblyTaskBoxComponentDto>()
            .ForMember(d => d.CatalogItemName, opt => opt.MapFrom(s => s.CatalogItem.FullName))
            .ForMember(d => d.CatalogItemType, opt => opt.MapFrom(s => s.CatalogItem.Type));

        CreateMap<AssemblyFulfillment, AssemblyFulfillmentDto>()
            .ForMember(d => d.SourceNodePath, opt => opt.MapFrom<FulfillmentNodePathResolver>())
            .ForMember(d => d.CreatedByName, opt => opt.MapFrom(s => s.CreatedBy != null ? s.CreatedBy.FullName : null))
            .ForMember(d => d.ResolvedCatalogItemName, opt => opt.MapFrom(s => s.ResolvedCatalogItem != null ? s.ResolvedCatalogItem.FullName : null))
            .ForMember(d => d.ResolvedCatalogItemType, opt => opt.MapFrom(s => s.ResolvedCatalogItem != null ? s.ResolvedCatalogItem.Type : (CatalogItemType?)null));

        CreateMap<AssemblyFulfillmentBundleComponent, AssemblyFulfillmentBundleComponentDto>()
            .ForMember(d => d.CatalogItemName, opt => opt.MapFrom(s => s.CatalogItem.FullName))
            .ForMember(d => d.CatalogItemType, opt => opt.MapFrom(s => s.CatalogItem.Type))
            .ForMember(d => d.SourceNodePath, opt => opt.MapFrom<FulfillmentBundleComponentNodePathResolver>());

        CreateMap<Order, AppEntity>()
            .ForMember(x => x.Type, opt => opt.MapFrom(_ => AppEntityType.Order))
            .ForMember(x => x.Name, opt => opt.MapFrom(s => "#" + s.Number))
            .ForMember(x => x.AdditionalFields, opt => opt.MapFrom(r => new Dictionary<string, object>
            {
                { "number", r.Number },
                { "type", r.Type },
                { "status", r.Status },
            }));

        CreateMap<Order, AppEntityWithSearchString>()
            .ForMember(x => x.AppEntity, opt => opt.MapFrom(x => x));

        // Marketplaces. MarketplaceAccountDto deliberately has no ApiKey member — only a mask.
        CreateMap<MarketplaceAccount, MarketplaceAccountSummaryDto>()
            .ForMember(d => d.WarehouseCount, opt => opt.MapFrom(s => s.Warehouses.Count))
            .ForMember(d => d.CardCount, opt => opt.MapFrom(s => s.Cards.Count))
            .ForMember(d => d.UnmappedCardCount,
                opt => opt.MapFrom(s => s.Cards.Count(c => c.CatalogItemId == null && !c.IsArchived)));
        
        CreateMap<MarketplaceAccount, MarketplaceAccountShortSummaryDto>();

        CreateMap<MarketplaceAccount, MarketplaceAccountDto>()
            .ForMember(d => d.CreatedByName, opt => opt.MapFrom(s => s.CreatedBy != null ? s.CreatedBy.UserName : null))
            .ForMember(d => d.WarehouseCount, opt => opt.MapFrom(s => s.Warehouses.Count))
            .ForMember(d => d.UnmappedWarehouseCount,
                opt => opt.MapFrom(s => s.Warehouses.Count(w => w.WarehouseId == null && !w.IsArchived)))
            .ForMember(d => d.CardCount, opt => opt.MapFrom(s => s.Cards.Count))
            .ForMember(d => d.UnmappedCardCount,
                opt => opt.MapFrom(s => s.Cards.Count(c => c.CatalogItemId == null && !c.IsArchived)))
            .ForMember(d => d.CredentialsUnreadable, opt => opt.Ignore())
            .ForMember(d => d.Capabilities, opt => opt.Ignore());

        CreateMap<MarketplaceWarehouse, MarketplaceWarehouseDto>()
            .ForMember(d => d.WarehouseName, opt => opt.MapFrom(s => s.Warehouse != null ? s.Warehouse.Name : null));

        CreateMap<MarketplaceCard, MarketplaceCardDto>()
            .ForMember(d => d.CatalogItemFullName,
                opt => opt.MapFrom(s => s.CatalogItem != null ? s.CatalogItem.FullName : null))
            .ForMember(d => d.CatalogItemArticle,
                opt => opt.MapFrom(s => s.CatalogItem != null ? s.CatalogItem.Article : null));

        CreateMap<MarketplaceSyncRun, MarketplaceSyncRunDto>()
            .ForMember(d => d.TriggeredByName,
                opt => opt.MapFrom(s => s.TriggeredBy != null ? s.TriggeredBy.UserName : null));

        CreateMap<ChangeLogEntry, ChangeLogEntryDto>()
            .ForMember(d => d.UserName, opt => opt.MapFrom(s => s.User != null ? s.User.UserName : "deleted"))
            .ForMember(d => d.Context, opt => opt.MapFrom(s =>
                s.Context != null ? JsonSerializer.Deserialize<JsonElement>(s.Context) : (JsonElement?)null))
            .ForMember(d => d.ActionData, opt => opt.MapFrom(s =>
                s.ActionData != null ? JsonSerializer.Deserialize<JsonElement>(s.ActionData) : (JsonElement?)null));
    }
}

/// <summary>
/// AutoMapper value resolver that builds the full breadcrumb source node path for a <see cref="WriteoffItem"/>.
/// Requires a pre-loaded <c>nodeById</c> dictionary passed via
/// <c>mapper.Map&lt;T&gt;(src, opts =&gt; opts.Items["nodeById"] = nodeById)</c>.
/// Falls back to a two-element path when the dictionary is not supplied.
/// </summary>
public class WriteoffItemNodePathResolver : IValueResolver<WriteoffItem, WriteoffItemDto, string[]>
{
    public string[] Resolve(
        WriteoffItem source,
        WriteoffItemDto destination,
        string[] destMember,
        ResolutionContext context)
    {
        if (context.TryGetItems(out var items)
            && items.TryGetValue("nodeById", out var obj)
            && obj is IReadOnlyDictionary<Guid, StoragePlaceNode> nodeById)
            return StoragePlaceNodeHelper.BuildPath(source.SourceNode, nodeById);

        return [source.SourceNode.RootStoragePlace.Name, source.SourceNode.Name];
    }
}

/// <summary>
/// Resolves the catalog item display name for a <see cref="WriteoffItem"/> regardless of its type.
/// </summary>
public class WriteoffItemCatalogNameResolver : IValueResolver<WriteoffItem, WriteoffItemDto, string>
{
    public string Resolve(
        WriteoffItem source,
        WriteoffItemDto destination,
        string destMember,
        ResolutionContext context)
    {
        if (source.CatalogItem is not null)
            return source.CatalogItem.Name;
        if (source.UnitInventoryItem?.CatalogItem is not null)
            return source.UnitInventoryItem.CatalogItem.Name;
        return string.Empty;
    }
}

/// <summary>
/// Builds the source node breadcrumb for an <see cref="AssemblyFulfillment"/>. Bundle fulfillments
/// carry no source node of their own (each component has one), so they resolve to an empty path.
/// Uses the same <c>nodeById</c> convention as <see cref="NodePathResolver"/>.
/// </summary>
public class FulfillmentNodePathResolver : IValueResolver<AssemblyFulfillment, AssemblyFulfillmentDto, string[]>
{
    public string[] Resolve(
        AssemblyFulfillment source,
        AssemblyFulfillmentDto destination,
        string[] destMember,
        ResolutionContext context)
    {
        if (source.SourceNode is null) return [];

        if (context.TryGetItems(out var items)
            && items.TryGetValue("nodeById", out var obj)
            && obj is IReadOnlyDictionary<Guid, StoragePlaceNode> nodeById)
            return StoragePlaceNodeHelper.BuildPath(source.SourceNode, nodeById);

        // RootStoragePlace is not loaded on a freshly created fulfillment.
        return source.SourceNode.RootStoragePlace is null
            ? [source.SourceNode.Name]
            : [source.SourceNode.RootStoragePlace.Name, source.SourceNode.Name];
    }
}

/// <summary>
/// Builds the source node breadcrumb for one component of a Bundle fulfillment.
/// Uses the same <c>nodeById</c> convention as <see cref="NodePathResolver"/>.
/// </summary>
public class FulfillmentBundleComponentNodePathResolver
    : IValueResolver<AssemblyFulfillmentBundleComponent, AssemblyFulfillmentBundleComponentDto, string[]>
{
    public string[] Resolve(
        AssemblyFulfillmentBundleComponent source,
        AssemblyFulfillmentBundleComponentDto destination,
        string[] destMember,
        ResolutionContext context)
    {
        if (source.SourceNode is null) return [];

        if (context.TryGetItems(out var items)
            && items.TryGetValue("nodeById", out var obj)
            && obj is IReadOnlyDictionary<Guid, StoragePlaceNode> nodeById)
            return StoragePlaceNodeHelper.BuildPath(source.SourceNode, nodeById);

        return source.SourceNode.RootStoragePlace is null
            ? [source.SourceNode.Name]
            : [source.SourceNode.RootStoragePlace.Name, source.SourceNode.Name];
    }
}

/// <summary>
/// AutoMapper value resolver that builds the full breadcrumb path for a <see cref="ReceiptItemPlacement"/>.
/// Requires a pre-loaded <c>nodeById</c> dictionary to be passed via
/// <c>mapper.Map&lt;T&gt;(src, opts =&gt; opts.Items["nodeById"] = nodeById)</c>.
/// Falls back to a two-element path <c>[StoragePlace, Node]</c> when the dictionary is not supplied
/// (e.g., changelog diffing where full ancestry is not needed).
/// </summary>
public class NodePathResolver : IValueResolver<ReceiptItemPlacement, ReceiptItemPlacementDto, string[]>
{
    public string[] Resolve(
        ReceiptItemPlacement source,
        ReceiptItemPlacementDto destination,
        string[] destMember,
        ResolutionContext context)
    {
        if (context.TryGetItems(out var items)
            && items.TryGetValue("nodeById", out var obj)
            && obj is IReadOnlyDictionary<Guid, StoragePlaceNode> nodeById)
            return StoragePlaceNodeHelper.BuildPath(source.StoragePlaceNode, nodeById);

        // Fallback: two-element path when no full node dictionary is available.
        return [source.StoragePlaceNode.RootStoragePlace.Name, source.StoragePlaceNode.Name];
    }
}
