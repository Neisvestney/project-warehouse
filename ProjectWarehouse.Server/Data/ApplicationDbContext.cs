using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ProjectWarehouse.Server.Domain;

namespace ProjectWarehouse.Server.Data;

public class ApplicationDbContext : IdentityDbContext<
    ApplicationUser,
    ApplicationRole,
    Guid,
    IdentityUserClaim<Guid>,
    ApplicationUserRole,
    IdentityUserLogin<Guid>,
    IdentityRoleClaim<Guid>,
    IdentityUserToken<Guid>>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<UserPermission> UserPermissions => Set<UserPermission>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    
    public DbSet<Warehouse> Warehouses => Set<Warehouse>();
    public DbSet<StoragePlace> StoragePlaces => Set<StoragePlace>();
    public DbSet<StoragePlaceNode> StoragePlacesNodes => Set<StoragePlaceNode>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUserRole>(e =>
        {
            e.HasOne(x => x.User).WithMany(x => x.UserRoles).HasForeignKey(x => x.UserId);
            e.HasOne(x => x.Role).WithMany(x => x.UserRoles).HasForeignKey(x => x.RoleId);
        });

        builder.Entity<RolePermission>(e =>
        {
            e.HasKey(x => new { x.RoleId, x.Permission });
            e.HasOne(x => x.Role).WithMany(x => x.RolePermissions).HasForeignKey(x => x.RoleId);
        });

        builder.Entity<UserPermission>(e =>
        {
            e.HasKey(x => new { x.UserId, x.Permission });
            e.HasOne(x => x.User).WithMany(x => x.UserPermissions).HasForeignKey(x => x.UserId);
        });

        builder.Entity<RefreshToken>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasOne(x => x.User).WithMany(x => x.RefreshTokens).HasForeignKey(x => x.UserId);
            e.Property(x => x.Token).HasMaxLength(256);
            e.Ignore(x => x.IsRevoked);
            e.Ignore(x => x.IsExpired);
            e.Ignore(x => x.IsActive);
        });
        
        builder.Entity<Warehouse>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasMany(x => x.StoragePlaces).WithOne(x => x.Warehouse).HasForeignKey(x => x.WarehouseId);
        });
        
        builder.Entity<StoragePlace>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasMany(x => x.StoragePlaceNodes).WithOne(x => x.RootStoragePlace).HasForeignKey(x => x.RootStoragePlaceId);
        });
        
        builder.Entity<StoragePlaceNode>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasOne(x => x.ParentNode).WithMany(x => x.ChildrenNodes).HasForeignKey(x => x.ParentNodeId);
        });
    }
}
