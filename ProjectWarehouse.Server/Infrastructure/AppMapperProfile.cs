using System.Text.Json;
using AutoMapper;
using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Models.ChangeLog;
using ProjectWarehouse.Server.Models;
using ProjectWarehouse.Server.Models.Catalog;
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
            .ForMember(d => d.DirectPermissions, opt => opt.MapFrom(s => s.UserPermissions.Select(up => up.Permission)));

        CreateMap<CatalogItemWithCharacteristic, CatalogItemCharacteristicDto>();
        CreateMap<CatalogItem, CatalogItemDto>();
        CreateMap<CatalogItem, NodeCatalogItemDto>();
        CreateMap<CatalogItemWithCharacteristic, NodeCharacteristicDto>();
        CreateMap<CatalogItem, CatalogItemSummaryDto>()
            .ForMember(d => d.CharacteristicCount, opt => opt.MapFrom(s => s.Characteristics.Count));

        CreateMap<WarehouseLayoutElement, WarehouseLayoutElementDto>();
        CreateMap<StoragePlace, StoragePlaceDto>();
        CreateMap<StoragePlaceNode, StoragePlaceNodeDto>();
        CreateMap<StoragePlaceNodeItemsGroup, ItemsGroupDto>();
        CreateMap<StoragePlaceNode, StoragePlaceNodeDetailsDto>();
        CreateMap<Warehouse, WarehouseDto>();
        CreateMap<Warehouse, WarehouseSummaryDto>()
            .ForMember(d => d.StoragePlaceCount, opt => opt.MapFrom(s => s.StoragePlaces.Count));

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
