using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectWarehouse.Server.Migrations
{
    /// <inheritdoc />
    public partial class RemoveAssembledBundle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AssemblyFulfillments_InventoryItems_AssembledBundleInventor~",
                table: "AssemblyFulfillments");

            migrationBuilder.DropForeignKey(
                name: "FK_CatalogItems_CatalogItems_SourceBundleId",
                table: "CatalogItems");

            migrationBuilder.DropForeignKey(
                name: "FK_ReceiptItemPlacements_InventoryItems_AssembledBundleInvento~",
                table: "ReceiptItemPlacements");

            migrationBuilder.DropForeignKey(
                name: "FK_WriteoffItems_InventoryItems_AssembledBundleInventoryItemId",
                table: "WriteoffItems");

            migrationBuilder.DropTable(
                name: "AssembledBundleComponents");

            migrationBuilder.DropTable(
                name: "AssembledBundleInventoryItemComponents");

            migrationBuilder.DropTable(
                name: "AssemblyFulfillmentAssembledBundleComponentSnapshots");

            migrationBuilder.DropIndex(
                name: "IX_WriteoffItems_AssembledBundleInventoryItemId",
                table: "WriteoffItems");

            migrationBuilder.DropIndex(
                name: "IX_ReceiptItemPlacements_AssembledBundleInventoryItemId",
                table: "ReceiptItemPlacements");

            migrationBuilder.DropIndex(
                name: "IX_CatalogItems_SourceBundleId",
                table: "CatalogItems");

            migrationBuilder.DropIndex(
                name: "IX_AssemblyFulfillments_AssembledBundleInventoryItemId",
                table: "AssemblyFulfillments");

            migrationBuilder.DropColumn(
                name: "AssembledBundleInventoryItemId",
                table: "WriteoffItems");

            migrationBuilder.DropColumn(
                name: "AssembledBundleInventoryItemId",
                table: "ReceiptItemPlacements");

            migrationBuilder.DropColumn(
                name: "SourceBundleId",
                table: "CatalogItems");

            migrationBuilder.DropColumn(
                name: "AssembledBundleInventoryItemId",
                table: "AssemblyFulfillments");

            migrationBuilder.AlterColumn<string>(
                name: "Type",
                table: "InventoryItems",
                type: "character varying(13)",
                maxLength: 13,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(21)",
                oldMaxLength: 21);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AssembledBundleInventoryItemId",
                table: "WriteoffItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AssembledBundleInventoryItemId",
                table: "ReceiptItemPlacements",
                type: "uuid",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Type",
                table: "InventoryItems",
                type: "character varying(21)",
                maxLength: 21,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(13)",
                oldMaxLength: 13);

            migrationBuilder.AddColumn<Guid>(
                name: "SourceBundleId",
                table: "CatalogItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AssembledBundleInventoryItemId",
                table: "AssemblyFulfillments",
                type: "uuid",
                nullable: true);

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
                name: "AssembledBundleInventoryItemComponents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AssembledBundleInventoryItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    CatalogItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    UnitInventoryItemId = table.Column<Guid>(type: "uuid", nullable: true),
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

            migrationBuilder.CreateTable(
                name: "AssemblyFulfillmentAssembledBundleComponentSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CatalogItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    FulfillmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    UnitInventoryItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    Quantity = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssemblyFulfillmentAssembledBundleComponentSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssemblyFulfillmentAssembledBundleComponentSnapshots_Assemb~",
                        column: x => x.FulfillmentId,
                        principalTable: "AssemblyFulfillments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AssemblyFulfillmentAssembledBundleComponentSnapshots_Catalo~",
                        column: x => x.CatalogItemId,
                        principalTable: "CatalogItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssemblyFulfillmentAssembledBundleComponentSnapshots_Invent~",
                        column: x => x.UnitInventoryItemId,
                        principalTable: "InventoryItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WriteoffItems_AssembledBundleInventoryItemId",
                table: "WriteoffItems",
                column: "AssembledBundleInventoryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ReceiptItemPlacements_AssembledBundleInventoryItemId",
                table: "ReceiptItemPlacements",
                column: "AssembledBundleInventoryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_CatalogItems_SourceBundleId",
                table: "CatalogItems",
                column: "SourceBundleId");

            migrationBuilder.CreateIndex(
                name: "IX_AssemblyFulfillments_AssembledBundleInventoryItemId",
                table: "AssemblyFulfillments",
                column: "AssembledBundleInventoryItemId");

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
                name: "IX_AssemblyFulfillmentAssembledBundleComponentSnapshots_Catalo~",
                table: "AssemblyFulfillmentAssembledBundleComponentSnapshots",
                column: "CatalogItemId");

            migrationBuilder.CreateIndex(
                name: "IX_AssemblyFulfillmentAssembledBundleComponentSnapshots_Fulfil~",
                table: "AssemblyFulfillmentAssembledBundleComponentSnapshots",
                column: "FulfillmentId");

            migrationBuilder.CreateIndex(
                name: "IX_AssemblyFulfillmentAssembledBundleComponentSnapshots_UnitIn~",
                table: "AssemblyFulfillmentAssembledBundleComponentSnapshots",
                column: "UnitInventoryItemId");

            migrationBuilder.AddForeignKey(
                name: "FK_AssemblyFulfillments_InventoryItems_AssembledBundleInventor~",
                table: "AssemblyFulfillments",
                column: "AssembledBundleInventoryItemId",
                principalTable: "InventoryItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_CatalogItems_CatalogItems_SourceBundleId",
                table: "CatalogItems",
                column: "SourceBundleId",
                principalTable: "CatalogItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ReceiptItemPlacements_InventoryItems_AssembledBundleInvento~",
                table: "ReceiptItemPlacements",
                column: "AssembledBundleInventoryItemId",
                principalTable: "InventoryItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_WriteoffItems_InventoryItems_AssembledBundleInventoryItemId",
                table: "WriteoffItems",
                column: "AssembledBundleInventoryItemId",
                principalTable: "InventoryItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
