using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectWarehouse.Server.Migrations
{
    /// <inheritdoc />
    public partial class RedesignCatalogItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ItemsGroup_CatalogItemsWithCharacteristics_CatalogItemWithC~",
                table: "ItemsGroup");

            migrationBuilder.DropTable(
                name: "CatalogItemsWithCharacteristics");

            migrationBuilder.RenameColumn(
                name: "CatalogItemWithCharacteristicId",
                table: "ItemsGroup",
                newName: "CatalogItemId");

            migrationBuilder.RenameIndex(
                name: "IX_ItemsGroup_CatalogItemWithCharacteristicId",
                table: "ItemsGroup",
                newName: "IX_ItemsGroup_CatalogItemId");

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "CatalogItems",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "GroupId",
                table: "CatalogItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SourceBundleId",
                table: "CatalogItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Type",
                table: "CatalogItems",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "AssembledBundleComponents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AssembledBundleId = table.Column<Guid>(type: "uuid", nullable: false),
                    ComponentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssembledBundleComponents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssembledBundleComponents_CatalogItems_AssembledBundleId",
                        column: x => x.AssembledBundleId,
                        principalTable: "CatalogItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AssembledBundleComponents_CatalogItems_ComponentId",
                        column: x => x.ComponentId,
                        principalTable: "CatalogItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BundleComponents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BundleId = table.Column<Guid>(type: "uuid", nullable: false),
                    ComponentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BundleComponents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BundleComponents_CatalogItems_BundleId",
                        column: x => x.BundleId,
                        principalTable: "CatalogItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BundleComponents_CatalogItems_ComponentId",
                        column: x => x.ComponentId,
                        principalTable: "CatalogItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CatalogItemTags",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatalogItemTags", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CatalogItemVariationMembers",
                columns: table => new
                {
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    VariationId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatalogItemVariationMembers", x => new { x.ItemId, x.VariationId });
                    table.ForeignKey(
                        name: "FK_CatalogItemVariationMembers_CatalogItems_ItemId",
                        column: x => x.ItemId,
                        principalTable: "CatalogItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CatalogItemVariationMembers_CatalogItems_VariationId",
                        column: x => x.VariationId,
                        principalTable: "CatalogItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InventoryItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CatalogItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    StoragePlaceNodeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(21)", maxLength: 21, nullable: false),
                    Sku = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InventoryItems_CatalogItems_CatalogItemId",
                        column: x => x.CatalogItemId,
                        principalTable: "CatalogItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryItems_StoragePlacesNodes_StoragePlaceNodeId",
                        column: x => x.StoragePlaceNodeId,
                        principalTable: "StoragePlacesNodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CatalogItemTagLinks",
                columns: table => new
                {
                    ItemsId = table.Column<Guid>(type: "uuid", nullable: false),
                    TagsId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatalogItemTagLinks", x => new { x.ItemsId, x.TagsId });
                    table.ForeignKey(
                        name: "FK_CatalogItemTagLinks_CatalogItemTags_TagsId",
                        column: x => x.TagsId,
                        principalTable: "CatalogItemTags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CatalogItemTagLinks_CatalogItems_ItemsId",
                        column: x => x.ItemsId,
                        principalTable: "CatalogItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AssembledBundleInventoryItemComponents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AssembledBundleInventoryItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    UnitInventoryItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    CatalogItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    Quantity = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssembledBundleInventoryItemComponents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssembledBundleInventoryItemComponents_CatalogItems_Catalog~",
                        column: x => x.CatalogItemId,
                        principalTable: "CatalogItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssembledBundleInventoryItemComponents_InventoryItems_Assem~",
                        column: x => x.AssembledBundleInventoryItemId,
                        principalTable: "InventoryItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AssembledBundleInventoryItemComponents_InventoryItems_UnitI~",
                        column: x => x.UnitInventoryItemId,
                        principalTable: "InventoryItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CatalogItems_GroupId",
                table: "CatalogItems",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_CatalogItems_SourceBundleId",
                table: "CatalogItems",
                column: "SourceBundleId");

            migrationBuilder.CreateIndex(
                name: "IX_AssembledBundleComponents_AssembledBundleId",
                table: "AssembledBundleComponents",
                column: "AssembledBundleId");

            migrationBuilder.CreateIndex(
                name: "IX_AssembledBundleComponents_ComponentId",
                table: "AssembledBundleComponents",
                column: "ComponentId");

            migrationBuilder.CreateIndex(
                name: "IX_AssembledBundleInventoryItemComponents_AssembledBundleInven~",
                table: "AssembledBundleInventoryItemComponents",
                column: "AssembledBundleInventoryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_AssembledBundleInventoryItemComponents_CatalogItemId",
                table: "AssembledBundleInventoryItemComponents",
                column: "CatalogItemId");

            migrationBuilder.CreateIndex(
                name: "IX_AssembledBundleInventoryItemComponents_UnitInventoryItemId",
                table: "AssembledBundleInventoryItemComponents",
                column: "UnitInventoryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_BundleComponents_BundleId",
                table: "BundleComponents",
                column: "BundleId");

            migrationBuilder.CreateIndex(
                name: "IX_BundleComponents_ComponentId",
                table: "BundleComponents",
                column: "ComponentId");

            migrationBuilder.CreateIndex(
                name: "IX_CatalogItemTagLinks_TagsId",
                table: "CatalogItemTagLinks",
                column: "TagsId");

            migrationBuilder.CreateIndex(
                name: "IX_CatalogItemVariationMembers_VariationId",
                table: "CatalogItemVariationMembers",
                column: "VariationId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryItems_CatalogItemId",
                table: "InventoryItems",
                column: "CatalogItemId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryItems_StoragePlaceNodeId",
                table: "InventoryItems",
                column: "StoragePlaceNodeId");

            migrationBuilder.AddForeignKey(
                name: "FK_CatalogItems_CatalogItems_GroupId",
                table: "CatalogItems",
                column: "GroupId",
                principalTable: "CatalogItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CatalogItems_CatalogItems_SourceBundleId",
                table: "CatalogItems",
                column: "SourceBundleId",
                principalTable: "CatalogItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ItemsGroup_CatalogItems_CatalogItemId",
                table: "ItemsGroup",
                column: "CatalogItemId",
                principalTable: "CatalogItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CatalogItems_CatalogItems_GroupId",
                table: "CatalogItems");

            migrationBuilder.DropForeignKey(
                name: "FK_CatalogItems_CatalogItems_SourceBundleId",
                table: "CatalogItems");

            migrationBuilder.DropForeignKey(
                name: "FK_ItemsGroup_CatalogItems_CatalogItemId",
                table: "ItemsGroup");

            migrationBuilder.DropTable(
                name: "AssembledBundleComponents");

            migrationBuilder.DropTable(
                name: "AssembledBundleInventoryItemComponents");

            migrationBuilder.DropTable(
                name: "BundleComponents");

            migrationBuilder.DropTable(
                name: "CatalogItemTagLinks");

            migrationBuilder.DropTable(
                name: "CatalogItemVariationMembers");

            migrationBuilder.DropTable(
                name: "InventoryItems");

            migrationBuilder.DropTable(
                name: "CatalogItemTags");

            migrationBuilder.DropIndex(
                name: "IX_CatalogItems_GroupId",
                table: "CatalogItems");

            migrationBuilder.DropIndex(
                name: "IX_CatalogItems_SourceBundleId",
                table: "CatalogItems");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "CatalogItems");

            migrationBuilder.DropColumn(
                name: "GroupId",
                table: "CatalogItems");

            migrationBuilder.DropColumn(
                name: "SourceBundleId",
                table: "CatalogItems");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "CatalogItems");

            migrationBuilder.RenameColumn(
                name: "CatalogItemId",
                table: "ItemsGroup",
                newName: "CatalogItemWithCharacteristicId");

            migrationBuilder.RenameIndex(
                name: "IX_ItemsGroup_CatalogItemId",
                table: "ItemsGroup",
                newName: "IX_ItemsGroup_CatalogItemWithCharacteristicId");

            migrationBuilder.CreateTable(
                name: "CatalogItemsWithCharacteristics",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CatalogItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Barcode = table.Column<string>(type: "text", nullable: true),
                    Characteristic = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatalogItemsWithCharacteristics", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CatalogItemsWithCharacteristics_CatalogItems_CatalogItemId",
                        column: x => x.CatalogItemId,
                        principalTable: "CatalogItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CatalogItemsWithCharacteristics_CatalogItemId",
                table: "CatalogItemsWithCharacteristics",
                column: "CatalogItemId");

            migrationBuilder.AddForeignKey(
                name: "FK_ItemsGroup_CatalogItemsWithCharacteristics_CatalogItemWithC~",
                table: "ItemsGroup",
                column: "CatalogItemWithCharacteristicId",
                principalTable: "CatalogItemsWithCharacteristics",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
