using AutoMapper;
using ProjectWarehouse.Server.Domain;
using ProjectWarehouse.Server.Models;
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

        CreateMap<StoragePlace, StoragePlaceDto>();
        CreateMap<StoragePlaceNode, StoragePlaceNodeDto>();
        CreateMap<Warehouse, WarehouseDto>();
        CreateMap<Warehouse, WarehouseSummaryDto>()
            .ForMember(d => d.StoragePlaceCount, opt => opt.MapFrom(s => s.StoragePlaces.Count));

        CreateMap<Warehouse, AppEntity>()
            .ForMember(x => x.Type, opt => opt.MapFrom(_ => AppEntityType.Warehouse))
            .ForMember(x => x.AdditionalFields, opt => opt.MapFrom(_ => (IReadOnlyDictionary<string, object>?)null));
    }
}
