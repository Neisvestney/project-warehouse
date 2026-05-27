using System.Text.Json;
using AutoMapper;
using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Models.ChangeLog;
using ProjectWarehouse.Server.Models;
using ProjectWarehouse.Server.Models.Catalog;
using ProjectWarehouse.Server.Models.Inventory;
using ProjectWarehouse.Server.Models.Roles;
using ProjectWarehouse.Server.Models.Users;
using ProjectWarehouse.Server.Models.Warehouses;

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
            .ForMember(d => d.ComponentName, opt => opt.MapFrom(s => s.Component.Name))
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
        CreateMap<CatalogItem, NodeCatalogItemDto>();

        CreateMap<WarehouseLayoutElement, WarehouseLayoutElementDto>();
        CreateMap<StoragePlace, StoragePlaceDto>();
        CreateMap<StoragePlaceNode, StoragePlaceNodeDto>();
        CreateMap<StoragePlaceNodeItemsGroup, ItemsGroupDto>();
        CreateMap<StoragePlaceNode, StoragePlaceNodeDetailsDto>()
            .ForMember(d => d.StoragePlaceId, opt => opt.MapFrom(s => s.RootStoragePlaceId));
        CreateMap<Warehouse, WarehouseDto>();
        CreateMap<Warehouse, WarehouseSummaryDto>()
            .ForMember(d => d.StoragePlaceCount, opt => opt.MapFrom(s => s.StoragePlaces.Count));

        CreateMap<UnitInventoryItem, UnitInventoryItemDto>()
            .ForMember(d => d.NodeId, opt => opt.MapFrom(s => s.StoragePlaceNodeId))
            .ForMember(d => d.NodeName, opt => opt.MapFrom(s => s.StoragePlaceNode.Name))
            .ForMember(d => d.StoragePlaceId, opt => opt.MapFrom(s => s.StoragePlaceNode.RootStoragePlaceId))
            .ForMember(d => d.StoragePlaceName, opt => opt.MapFrom(s => s.StoragePlaceNode.RootStoragePlace.Name))
            .ForMember(d => d.WarehouseId, opt => opt.MapFrom(s => s.StoragePlaceNode.RootStoragePlace.WarehouseId))
            .ForMember(d => d.WarehouseName, opt => opt.MapFrom(s => s.StoragePlaceNode.RootStoragePlace.Warehouse.Name));

        CreateMap<AssembledBundleInventoryItem, AssembledBundleInventoryItemDto>()
            .ForMember(d => d.NodeId, opt => opt.MapFrom(s => s.StoragePlaceNodeId))
            .ForMember(d => d.NodeName, opt => opt.MapFrom(s => s.StoragePlaceNode.Name))
            .ForMember(d => d.StoragePlaceId, opt => opt.MapFrom(s => s.StoragePlaceNode.RootStoragePlaceId))
            .ForMember(d => d.StoragePlaceName, opt => opt.MapFrom(s => s.StoragePlaceNode.RootStoragePlace.Name))
            .ForMember(d => d.WarehouseId, opt => opt.MapFrom(s => s.StoragePlaceNode.RootStoragePlace.WarehouseId))
            .ForMember(d => d.WarehouseName, opt => opt.MapFrom(s => s.StoragePlaceNode.RootStoragePlace.Warehouse.Name));

        CreateMap<Warehouse, AppEntity>()
            .ForMember(x => x.Type, opt => opt.MapFrom(_ => AppEntityType.Warehouse))
            .ForMember(x => x.AdditionalFields, opt => opt.MapFrom(_ => (IReadOnlyDictionary<string, object>?)null));

        CreateMap<ChangeLogEntry, ChangeLogEntryDto>()
            .ForMember(d => d.UserName, opt => opt.MapFrom(s => s.User != null ? s.User.UserName : "deleted"))
            .ForMember(d => d.Context, opt => opt.MapFrom(s =>
                s.Context != null ? JsonSerializer.Deserialize<JsonElement>(s.Context) : (JsonElement?)null))
            .ForMember(d => d.ActionData, opt => opt.MapFrom(s =>
                s.ActionData != null ? JsonSerializer.Deserialize<JsonElement>(s.ActionData) : (JsonElement?)null));
    }
}
