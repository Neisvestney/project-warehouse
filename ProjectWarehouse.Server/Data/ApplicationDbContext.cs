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

    public DbSet<ChangeLogEntry> ChangeLogEntries => Set<ChangeLogEntry>();

    public DbSet<DataFile> DataFiles => Set<DataFile>();

    public DbSet<CatalogItem> CatalogItems => Set<CatalogItem>();
    public DbSet<CatalogItemTag> CatalogItemTags => Set<CatalogItemTag>();
    public DbSet<CatalogItemImage> CatalogItemImages => Set<CatalogItemImage>();
    public DbSet<CatalogItemVariationMember> CatalogItemVariationMembers => Set<CatalogItemVariationMember>();
    public DbSet<BundleComponent> BundleComponents => Set<BundleComponent>();

    public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();

    public DbSet<Warehouse> Warehouses => Set<Warehouse>();
    public DbSet<StoragePlace> StoragePlaces => Set<StoragePlace>();
    public DbSet<StoragePlaceNode> StoragePlacesNodes => Set<StoragePlaceNode>();
    public DbSet<StoragePlaceNodeItemsGroup> StoragePlacesNodesItemsGroups => Set<StoragePlaceNodeItemsGroup>();

    public DbSet<StockMovement> StockMovements => Set<StockMovement>();

    public DbSet<Receipt> Receipts => Set<Receipt>();
    public DbSet<ReceiptItem> ReceiptItems => Set<ReceiptItem>();
    public DbSet<ReceiptItemPlacement> ReceiptItemPlacements => Set<ReceiptItemPlacement>();

    public DbSet<Writeoff> Writeoffs => Set<Writeoff>();
    public DbSet<WriteoffItem> WriteoffItems => Set<WriteoffItem>();

    public DbSet<Stocktake> Stocktakes => Set<Stocktake>();
    public DbSet<StocktakeNode> StocktakeNodes => Set<StocktakeNode>();
    public DbSet<StocktakeItem> StocktakeItems => Set<StocktakeItem>();

    public DbSet<MarketplaceAccount> MarketplaceAccounts => Set<MarketplaceAccount>();
    public DbSet<MarketplaceWarehouse> MarketplaceWarehouses => Set<MarketplaceWarehouse>();
    public DbSet<MarketplaceCard> MarketplaceCards => Set<MarketplaceCard>();
    public DbSet<MarketplaceSyncRun> MarketplaceSyncRuns => Set<MarketplaceSyncRun>();
    public DbSet<MarketplaceOrder> MarketplaceOrders => Set<MarketplaceOrder>();

    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderMarketplaceItem> OrderMarketplaceItems => Set<OrderMarketplaceItem>();
    public DbSet<OrderBox> OrderBoxes => Set<OrderBox>();
    public DbSet<OrderBoxComponent> OrderBoxComponents => Set<OrderBoxComponent>();
    public DbSet<AssemblyTask> AssemblyTasks => Set<AssemblyTask>();
    public DbSet<AssemblyTaskBox> AssemblyTaskBoxes => Set<AssemblyTaskBox>();
    public DbSet<AssemblyTaskBoxComponent> AssemblyTaskBoxComponents => Set<AssemblyTaskBoxComponent>();
    public DbSet<AssemblyFulfillment> AssemblyFulfillments => Set<AssemblyFulfillment>();
    public DbSet<AssemblyFulfillmentBundleComponent> AssemblyFulfillmentBundleComponents =>
        Set<AssemblyFulfillmentBundleComponent>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>(e => { e.HasMany(x => x.AssignedWarehouses).WithMany(x => x.AssignedUsers); });

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

        builder.Entity<ChangeLogEntry>(e =>
        {
            e.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<CatalogItem>(e =>
        {
            e.HasKey(x => x.Id);

            e.HasMany(x => x.GroupChildren)
                .WithOne(x => x.Group)
                .HasForeignKey(x => x.GroupId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasMany(x => x.Tags)
                .WithMany(x => x.Items)
                .UsingEntity("CatalogItemTagLinks");

            // Restrict, never Cascade: deleting a file must not delete the item that uses it
            e.HasOne(x => x.MainImageFile)
                .WithMany()
                .HasForeignKey(x => x.MainImageFileId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<CatalogItemImage>(e =>
        {
            e.HasKey(x => x.Id);

            e.HasOne(x => x.CatalogItem)
                .WithMany(x => x.Images)
                .HasForeignKey(x => x.CatalogItemId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.DataFile)
                .WithMany()
                .HasForeignKey(x => x.DataFileId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasIndex(x => new { x.CatalogItemId, x.Order });
        });

        builder.Entity<CatalogItemVariationMember>(e =>
        {
            e.HasKey(x => new { x.ItemId, x.VariationId });

            e.HasOne(x => x.Item)
                .WithMany(x => x.VariationMemberships)
                .HasForeignKey(x => x.ItemId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.Variation)
                .WithMany(x => x.VariationMembers)
                .HasForeignKey(x => x.VariationId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<BundleComponent>(e =>
        {
            e.HasKey(x => x.Id);

            e.HasOne(x => x.Bundle)
                .WithMany(x => x.BundleComponents)
                .HasForeignKey(x => x.BundleId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.Component)
                .WithMany()
                .HasForeignKey(x => x.ComponentId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ItemsGroup>(e => { e.HasKey(x => x.Id); });

        builder.Entity<StoragePlaceNodeItemsGroup>(e =>
        {
            e.HasOne(x => x.CatalogItem)
                .WithMany()
                .HasForeignKey(x => x.CatalogItemId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.StoragePlaceNode)
                .WithMany(x => x.ItemsGroups)
                .HasForeignKey(x => x.StoragePlaceNodeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<StockMovement>(e =>
        {
            e.HasKey(x => x.Id);

            e.Property(x => x.Action).HasMaxLength(64);

            e.HasOne(x => x.CatalogItem)
                .WithMany()
                .HasForeignKey(x => x.CatalogItemId)
                .OnDelete(DeleteBehavior.Restrict);

            // Location and author are audit references — deleting a warehouse must not erase its history
            e.HasOne(x => x.StoragePlaceNode)
                .WithMany()
                .HasForeignKey(x => x.StoragePlaceNodeId)
                .OnDelete(DeleteBehavior.SetNull);

            e.HasOne(x => x.StoragePlace)
                .WithMany()
                .HasForeignKey(x => x.StoragePlaceId)
                .OnDelete(DeleteBehavior.SetNull);

            e.HasOne(x => x.Warehouse)
                .WithMany()
                .HasForeignKey(x => x.WarehouseId)
                .OnDelete(DeleteBehavior.SetNull);

            e.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<InventoryItem>(e =>
        {
            e.HasKey(x => x.Id);

            e.HasDiscriminator<string>("Type")
                .HasValue<UnitInventoryItem>("Unit");

            e.HasOne(x => x.CatalogItem)
                .WithMany()
                .HasForeignKey(x => x.CatalogItemId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.StoragePlaceNode)
                .WithMany(x => x.InventoryItems)
                .HasForeignKey(x => x.StoragePlaceNodeId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Partial unique index: inventory number must be unique per catalog item among Unit items only.
        builder.Entity<UnitInventoryItem>(e =>
        {
            e.HasIndex(x => new { x.CatalogItemId, x.InventoryNumber })
                .IsUnique()
                .HasFilter("\"Type\" = 'Unit'");
        });

        builder.Entity<Warehouse>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasMany(x => x.StoragePlaces).WithOne(x => x.Warehouse).HasForeignKey(x => x.WarehouseId);
            e.OwnsMany(x => x.LayoutObjects, lo => { lo.ToJson(); });
            e.HasOne(x => x.DefaultStoragePlaceNode)
                .WithMany()
                .HasForeignKey(x => x.DefaultStoragePlaceNodeId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<StoragePlace>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasMany(x => x.StoragePlaceNodes).WithOne(x => x.RootStoragePlace)
                .HasForeignKey(x => x.RootStoragePlaceId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<StoragePlaceNode>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasOne(x => x.ParentNode).WithMany(x => x.ChildrenNodes).HasForeignKey(x => x.ParentNodeId);
        });

        builder.Entity<Receipt>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Number).ValueGeneratedOnAdd();
            e.HasIndex(x => x.Number).IsUnique();

            e.HasOne(x => x.Warehouse)
                .WithMany()
                .HasForeignKey(x => x.WarehouseId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.CreatedBy)
                .WithMany()
                .HasForeignKey(x => x.CreatedById)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<ReceiptItem>(e =>
        {
            e.HasKey(x => x.Id);

            e.HasOne(x => x.Receipt)
                .WithMany(x => x.Items)
                .HasForeignKey(x => x.ReceiptId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.CatalogItem)
                .WithMany()
                .HasForeignKey(x => x.CatalogItemId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ReceiptItemPlacement>(e =>
        {
            e.HasKey(x => x.Id);

            e.HasOne(x => x.ReceiptItem)
                .WithMany(x => x.Placements)
                .HasForeignKey(x => x.ReceiptItemId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.StoragePlaceNode)
                .WithMany()
                .HasForeignKey(x => x.StoragePlaceNodeId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.UnitInventoryItem)
                .WithMany()
                .HasForeignKey(x => x.UnitInventoryItemId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<Writeoff>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Number).ValueGeneratedOnAdd();
            e.HasIndex(x => x.Number).IsUnique();

            e.HasOne(x => x.Warehouse)
                .WithMany()
                .HasForeignKey(x => x.WarehouseId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.CreatedBy)
                .WithMany()
                .HasForeignKey(x => x.CreatedById)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<WriteoffItem>(e =>
        {
            e.HasKey(x => x.Id);

            e.HasOne(x => x.Writeoff)
                .WithMany(x => x.Items)
                .HasForeignKey(x => x.WriteoffId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.SourceNode)
                .WithMany()
                .HasForeignKey(x => x.SourceNodeId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.CatalogItem)
                .WithMany()
                .HasForeignKey(x => x.CatalogItemId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.UnitInventoryItem)
                .WithMany()
                .HasForeignKey(x => x.UnitInventoryItemId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<Stocktake>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Number).ValueGeneratedOnAdd();
            e.HasIndex(x => x.Number).IsUnique();

            e.HasOne(x => x.Warehouse)
                .WithMany()
                .HasForeignKey(x => x.WarehouseId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.CreatedBy)
                .WithMany()
                .HasForeignKey(x => x.CreatedById)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<StocktakeNode>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.StocktakeId, x.StoragePlaceNodeId }).IsUnique();

            e.HasOne(x => x.Stocktake)
                .WithMany(x => x.Nodes)
                .HasForeignKey(x => x.StocktakeId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.StoragePlaceNode)
                .WithMany()
                .HasForeignKey(x => x.StoragePlaceNodeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<StocktakeItem>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.InventoryNumber).HasMaxLength(128);

            e.HasOne(x => x.StocktakeNode)
                .WithMany(x => x.Items)
                .HasForeignKey(x => x.StocktakeNodeId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.CatalogItem)
                .WithMany()
                .HasForeignKey(x => x.CatalogItemId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.UnitInventoryItem)
                .WithMany()
                .HasForeignKey(x => x.UnitInventoryItemId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<Order>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Number).ValueGeneratedOnAdd();
            e.HasIndex(x => x.Number).IsUnique();

            e.HasOne(x => x.Warehouse)
                .WithMany()
                .HasForeignKey(x => x.WarehouseId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.CreatedBy)
                .WithMany()
                .HasForeignKey(x => x.CreatedById)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<OrderMarketplaceItem>(e =>
        {
            e.HasKey(x => x.Id);

            e.HasOne(x => x.Order)
                .WithMany(x => x.MarketplaceItems)
                .HasForeignKey(x => x.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.MarketplaceCard)
                .WithMany()
                .HasForeignKey(x => x.MarketplaceCardId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<OrderBox>(e =>
        {
            e.HasKey(x => x.Id);

            e.HasOne(x => x.Order)
                .WithMany(x => x.Boxes)
                .HasForeignKey(x => x.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<OrderBoxComponent>(e =>
        {
            e.HasKey(x => x.Id);

            e.HasOne(x => x.OrderBox)
                .WithMany(x => x.Components)
                .HasForeignKey(x => x.OrderBoxId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.CatalogItem)
                .WithMany()
                .HasForeignKey(x => x.CatalogItemId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<AssemblyTask>(e =>
        {
            e.HasKey(x => x.Id);

            e.HasOne(x => x.Order)
                .WithMany(x => x.AssemblyTasks)
                .HasForeignKey(x => x.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.AssignedTo)
                .WithMany()
                .HasForeignKey(x => x.AssignedToId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<AssemblyTaskBox>(e =>
        {
            e.HasKey(x => x.Id);

            e.HasOne(x => x.AssemblyTask)
                .WithMany(x => x.Boxes)
                .HasForeignKey(x => x.AssemblyTaskId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.OrderBox)
                .WithMany()
                .HasForeignKey(x => x.OrderBoxId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<AssemblyTaskBoxComponent>(e =>
        {
            e.HasKey(x => x.Id);

            e.HasOne(x => x.AssemblyTaskBox)
                .WithMany(x => x.Components)
                .HasForeignKey(x => x.AssemblyTaskBoxId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.CatalogItem)
                .WithMany()
                .HasForeignKey(x => x.CatalogItemId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<AssemblyFulfillment>(e =>
        {
            e.HasKey(x => x.Id);

            e.HasOne(x => x.TaskBoxComponent)
                .WithMany(x => x.Fulfillments)
                .HasForeignKey(x => x.TaskBoxComponentId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.SourceNode)
                .WithMany()
                .HasForeignKey(x => x.SourceNodeId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.UnitInventoryItem)
                .WithMany()
                .HasForeignKey(x => x.UnitInventoryItemId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);

            e.HasOne(x => x.ResolvedCatalogItem)
                .WithMany()
                .HasForeignKey(x => x.ResolvedCatalogItemId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.CreatedBy)
                .WithMany()
                .HasForeignKey(x => x.CreatedById)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<AssemblyFulfillmentBundleComponent>(e =>
        {
            e.HasKey(x => x.Id);

            e.HasOne(x => x.Fulfillment)
                .WithMany(x => x.BundleComponents)
                .HasForeignKey(x => x.FulfillmentId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.CatalogItem)
                .WithMany()
                .HasForeignKey(x => x.CatalogItemId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.SourceNode)
                .WithMany()
                .HasForeignKey(x => x.SourceNodeId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.UnitInventoryItem)
                .WithMany()
                .HasForeignKey(x => x.UnitInventoryItemId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<MarketplaceAccount>(e =>
        {
            e.HasKey(x => x.Id);

            e.HasOne(x => x.CreatedBy)
                .WithMany()
                .HasForeignKey(x => x.CreatedById)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);

            e.HasIndex(x => x.Type);
        });

        builder.Entity<MarketplaceWarehouse>(e =>
        {
            e.HasKey(x => x.Id);

            e.HasOne(x => x.MarketplaceAccount)
                .WithMany(x => x.Warehouses)
                .HasForeignKey(x => x.MarketplaceAccountId)
                .OnDelete(DeleteBehavior.Cascade);

            // Restrict, not SetNull: deleting a mapped WMS warehouse must be blocked, not silently unmapped
            e.HasOne(x => x.Warehouse)
                .WithMany(x => x.MarketplaceWarehouses)
                .HasForeignKey(x => x.WarehouseId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasIndex(x => new { x.MarketplaceAccountId, x.ExternalId }).IsUnique();
            e.HasIndex(x => x.WarehouseId);
        });

        builder.Entity<MarketplaceCard>(e =>
        {
            e.HasKey(x => x.Id);

            e.HasOne(x => x.MarketplaceAccount)
                .WithMany(x => x.Cards)
                .HasForeignKey(x => x.MarketplaceAccountId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.CatalogItem)
                .WithMany(x => x.MarketplaceCards)
                .HasForeignKey(x => x.CatalogItemId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);

            e.Property(x => x.Price).HasPrecision(18, 2);

            e.HasIndex(x => new { x.MarketplaceAccountId, x.ExternalId }).IsUnique();
            e.HasIndex(x => new { x.MarketplaceAccountId, x.OfferId });
            // postings carry sku, never product_id, so order sync resolves cards through this index
            e.HasIndex(x => new { x.MarketplaceAccountId, x.Sku });
            e.HasIndex(x => x.CatalogItemId);
        });

        builder.Entity<MarketplaceSyncRun>(e =>
        {
            e.HasKey(x => x.Id);

            e.HasOne(x => x.MarketplaceAccount)
                .WithMany(x => x.SyncRuns)
                .HasForeignKey(x => x.MarketplaceAccountId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.TriggeredBy)
                .WithMany()
                .HasForeignKey(x => x.TriggeredById)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);

            e.HasIndex(x => new { x.MarketplaceAccountId, x.StartedAt })
                .IsDescending(false, true);

            // Explicit, unlike the single AppFieldError above: a collection of a complex type is also a
            // candidate for EF's owned-collection discovery, and [Column] alone does not settle that.
            e.Property(x => x.SkippedOrders).HasColumnType("jsonb");
        });

        builder.Entity<MarketplaceOrder>(e =>
        {
            // Shared primary key with Order: strictly 1:1, never addressed on its own
            e.HasKey(x => x.OrderId);
            e.Property(x => x.OrderId).ValueGeneratedNever();

            e.HasOne(x => x.Order)
                .WithOne(x => x.MarketplaceOrder)
                .HasForeignKey<MarketplaceOrder>(x => x.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            // Restrict: cascading here would delete orders along with their assembly history and
            // stock movements. The controller turns the conflict into 409 marketplaceAccountHasOrders.
            e.HasOne(x => x.MarketplaceAccount)
                .WithMany(x => x.Orders)
                .HasForeignKey(x => x.MarketplaceAccountId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.LabelFile)
                .WithMany()
                .HasForeignKey(x => x.LabelFileId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasIndex(x => new { x.MarketplaceAccountId, x.PostingNumber }).IsUnique();
            e.HasIndex(x => new { x.MarketplaceAccountId, x.Status });
        });

        builder.Entity<DataFile>(e =>
        {
            e.HasKey(x => x.Id);

            e.Property(x => x.StorageKey).HasMaxLength(256);
            e.Property(x => x.OriginalFileName).HasMaxLength(256);
            e.Property(x => x.ContentType).HasMaxLength(128);

            e.HasIndex(x => x.StorageKey).IsUnique();
            e.HasIndex(x => x.CreatedAt);

            e.HasOne(x => x.CreatedBy)
                .WithMany()
                .HasForeignKey(x => x.CreatedById)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // Npgsql requires DateTime values with Kind=Utc for "timestamp with time zone" columns.
        // Values coming from JSON model binding are deserialized with Kind=Unspecified, which
        // otherwise throws at save time. Treat unspecified/local kinds as UTC transparently.
        var utcConverter = new Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<DateTime, DateTime>(
            v => v.Kind == DateTimeKind.Utc ? v : DateTime.SpecifyKind(v, DateTimeKind.Utc),
            v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(DateTime) || property.ClrType == typeof(DateTime?))
                    property.SetValueConverter(utcConverter);
            }
        }
    }
}
