using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ProjectWarehouse.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddInboundOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "InboundOrderDeclaredItemsGroup_InboundOrderId",
                table: "ItemsGroup",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "InboundOrderId",
                table: "ItemsGroup",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "InboundOrders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: true),
                    PlannedStartDateTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InboundOrders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InboundOrders_Warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalTable: "Warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ApplicationUserInboundOrder",
                columns: table => new
                {
                    AssignedInboundOrdersId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignedUsersId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationUserInboundOrder", x => new { x.AssignedInboundOrdersId, x.AssignedUsersId });
                    table.ForeignKey(
                        name: "FK_ApplicationUserInboundOrder_AspNetUsers_AssignedUsersId",
                        column: x => x.AssignedUsersId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ApplicationUserInboundOrder_InboundOrders_AssignedInboundOr~",
                        column: x => x.AssignedInboundOrdersId,
                        principalTable: "InboundOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InboundOrderDraftItemsGroup",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Article = table.Column<string>(type: "text", nullable: false),
                    Barcode = table.Column<string>(type: "text", nullable: true),
                    RootBarcode = table.Column<string>(type: "text", nullable: true),
                    Characteristic = table.Column<string>(type: "text", nullable: false),
                    Count = table.Column<int>(type: "integer", nullable: false),
                    CatalogItemWithCharacteristicId = table.Column<Guid>(type: "uuid", nullable: true),
                    InboundOrderId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InboundOrderDraftItemsGroup", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InboundOrderDraftItemsGroup_CatalogItemsWithCharacteristics~",
                        column: x => x.CatalogItemWithCharacteristicId,
                        principalTable: "CatalogItemsWithCharacteristics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_InboundOrderDraftItemsGroup_InboundOrders_InboundOrderId",
                        column: x => x.InboundOrderId,
                        principalTable: "InboundOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ItemsGroup_InboundOrderDeclaredItemsGroup_InboundOrderId",
                table: "ItemsGroup",
                column: "InboundOrderDeclaredItemsGroup_InboundOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemsGroup_InboundOrderId",
                table: "ItemsGroup",
                column: "InboundOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationUserInboundOrder_AssignedUsersId",
                table: "ApplicationUserInboundOrder",
                column: "AssignedUsersId");

            migrationBuilder.CreateIndex(
                name: "IX_InboundOrderDraftItemsGroup_CatalogItemWithCharacteristicId",
                table: "InboundOrderDraftItemsGroup",
                column: "CatalogItemWithCharacteristicId");

            migrationBuilder.CreateIndex(
                name: "IX_InboundOrderDraftItemsGroup_InboundOrderId",
                table: "InboundOrderDraftItemsGroup",
                column: "InboundOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_InboundOrders_Number",
                table: "InboundOrders",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InboundOrders_WarehouseId",
                table: "InboundOrders",
                column: "WarehouseId");

            migrationBuilder.AddForeignKey(
                name: "FK_ItemsGroup_InboundOrders_InboundOrderDeclaredItemsGroup_Inb~",
                table: "ItemsGroup",
                column: "InboundOrderDeclaredItemsGroup_InboundOrderId",
                principalTable: "InboundOrders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ItemsGroup_InboundOrders_InboundOrderId",
                table: "ItemsGroup",
                column: "InboundOrderId",
                principalTable: "InboundOrders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ItemsGroup_InboundOrders_InboundOrderDeclaredItemsGroup_Inb~",
                table: "ItemsGroup");

            migrationBuilder.DropForeignKey(
                name: "FK_ItemsGroup_InboundOrders_InboundOrderId",
                table: "ItemsGroup");

            migrationBuilder.DropTable(
                name: "ApplicationUserInboundOrder");

            migrationBuilder.DropTable(
                name: "InboundOrderDraftItemsGroup");

            migrationBuilder.DropTable(
                name: "InboundOrders");

            migrationBuilder.DropIndex(
                name: "IX_ItemsGroup_InboundOrderDeclaredItemsGroup_InboundOrderId",
                table: "ItemsGroup");

            migrationBuilder.DropIndex(
                name: "IX_ItemsGroup_InboundOrderId",
                table: "ItemsGroup");

            migrationBuilder.DropColumn(
                name: "InboundOrderDeclaredItemsGroup_InboundOrderId",
                table: "ItemsGroup");

            migrationBuilder.DropColumn(
                name: "InboundOrderId",
                table: "ItemsGroup");
        }
    }
}
