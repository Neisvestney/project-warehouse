using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ProjectWarehouse.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddOrders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Orders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    PlannedShipmentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    MarketplaceOrderId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Orders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Orders_AspNetUsers_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Orders_Warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalTable: "Warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AssemblyTasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignedToId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssemblyTasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssemblyTasks_AspNetUsers_AssignedToId",
                        column: x => x.AssignedToId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_AssemblyTasks_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrderBoxes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    Label = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderBoxes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderBoxes_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrderMarketplaceItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    MarketplaceCardId = table.Column<string>(type: "text", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderMarketplaceItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderMarketplaceItems_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AssemblyTaskBoxes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AssemblyTaskId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderBoxId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssemblyTaskBoxes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssemblyTaskBoxes_AssemblyTasks_AssemblyTaskId",
                        column: x => x.AssemblyTaskId,
                        principalTable: "AssemblyTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AssemblyTaskBoxes_OrderBoxes_OrderBoxId",
                        column: x => x.OrderBoxId,
                        principalTable: "OrderBoxes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OrderBoxComponents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderBoxId = table.Column<Guid>(type: "uuid", nullable: false),
                    CatalogItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderBoxComponents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderBoxComponents_CatalogItems_CatalogItemId",
                        column: x => x.CatalogItemId,
                        principalTable: "CatalogItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrderBoxComponents_OrderBoxes_OrderBoxId",
                        column: x => x.OrderBoxId,
                        principalTable: "OrderBoxes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AssemblyTaskBoxComponents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AssemblyTaskBoxId = table.Column<Guid>(type: "uuid", nullable: false),
                    CatalogItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssemblyTaskBoxComponents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssemblyTaskBoxComponents_AssemblyTaskBoxes_AssemblyTaskBox~",
                        column: x => x.AssemblyTaskBoxId,
                        principalTable: "AssemblyTaskBoxes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AssemblyTaskBoxComponents_CatalogItems_CatalogItemId",
                        column: x => x.CatalogItemId,
                        principalTable: "CatalogItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AssemblyFulfillments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TaskBoxComponentId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceNodeId = table.Column<Guid>(type: "uuid", nullable: true),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    UnitInventoryItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    UnitInventoryNumber = table.Column<string>(type: "text", nullable: true),
                    AssembledBundleInventoryItemId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssemblyFulfillments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssemblyFulfillments_AssemblyTaskBoxComponents_TaskBoxCompo~",
                        column: x => x.TaskBoxComponentId,
                        principalTable: "AssemblyTaskBoxComponents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AssemblyFulfillments_InventoryItems_AssembledBundleInventor~",
                        column: x => x.AssembledBundleInventoryItemId,
                        principalTable: "InventoryItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_AssemblyFulfillments_InventoryItems_UnitInventoryItemId",
                        column: x => x.UnitInventoryItemId,
                        principalTable: "InventoryItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_AssemblyFulfillments_StoragePlacesNodes_SourceNodeId",
                        column: x => x.SourceNodeId,
                        principalTable: "StoragePlacesNodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AssemblyFulfillmentAssembledBundleComponentSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FulfillmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    UnitInventoryItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    CatalogItemId = table.Column<Guid>(type: "uuid", nullable: true),
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

            migrationBuilder.CreateTable(
                name: "AssemblyFulfillmentBundleComponents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FulfillmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    CatalogItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceNodeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    UnitInventoryItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    UnitInventoryNumber = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssemblyFulfillmentBundleComponents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssemblyFulfillmentBundleComponents_AssemblyFulfillments_Fu~",
                        column: x => x.FulfillmentId,
                        principalTable: "AssemblyFulfillments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AssemblyFulfillmentBundleComponents_CatalogItems_CatalogIte~",
                        column: x => x.CatalogItemId,
                        principalTable: "CatalogItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssemblyFulfillmentBundleComponents_InventoryItems_UnitInve~",
                        column: x => x.UnitInventoryItemId,
                        principalTable: "InventoryItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_AssemblyFulfillmentBundleComponents_StoragePlacesNodes_Sour~",
                        column: x => x.SourceNodeId,
                        principalTable: "StoragePlacesNodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

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

            migrationBuilder.CreateIndex(
                name: "IX_AssemblyFulfillmentBundleComponents_CatalogItemId",
                table: "AssemblyFulfillmentBundleComponents",
                column: "CatalogItemId");

            migrationBuilder.CreateIndex(
                name: "IX_AssemblyFulfillmentBundleComponents_FulfillmentId",
                table: "AssemblyFulfillmentBundleComponents",
                column: "FulfillmentId");

            migrationBuilder.CreateIndex(
                name: "IX_AssemblyFulfillmentBundleComponents_SourceNodeId",
                table: "AssemblyFulfillmentBundleComponents",
                column: "SourceNodeId");

            migrationBuilder.CreateIndex(
                name: "IX_AssemblyFulfillmentBundleComponents_UnitInventoryItemId",
                table: "AssemblyFulfillmentBundleComponents",
                column: "UnitInventoryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_AssemblyFulfillments_AssembledBundleInventoryItemId",
                table: "AssemblyFulfillments",
                column: "AssembledBundleInventoryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_AssemblyFulfillments_SourceNodeId",
                table: "AssemblyFulfillments",
                column: "SourceNodeId");

            migrationBuilder.CreateIndex(
                name: "IX_AssemblyFulfillments_TaskBoxComponentId",
                table: "AssemblyFulfillments",
                column: "TaskBoxComponentId");

            migrationBuilder.CreateIndex(
                name: "IX_AssemblyFulfillments_UnitInventoryItemId",
                table: "AssemblyFulfillments",
                column: "UnitInventoryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_AssemblyTaskBoxComponents_AssemblyTaskBoxId",
                table: "AssemblyTaskBoxComponents",
                column: "AssemblyTaskBoxId");

            migrationBuilder.CreateIndex(
                name: "IX_AssemblyTaskBoxComponents_CatalogItemId",
                table: "AssemblyTaskBoxComponents",
                column: "CatalogItemId");

            migrationBuilder.CreateIndex(
                name: "IX_AssemblyTaskBoxes_AssemblyTaskId",
                table: "AssemblyTaskBoxes",
                column: "AssemblyTaskId");

            migrationBuilder.CreateIndex(
                name: "IX_AssemblyTaskBoxes_OrderBoxId",
                table: "AssemblyTaskBoxes",
                column: "OrderBoxId");

            migrationBuilder.CreateIndex(
                name: "IX_AssemblyTasks_AssignedToId",
                table: "AssemblyTasks",
                column: "AssignedToId");

            migrationBuilder.CreateIndex(
                name: "IX_AssemblyTasks_OrderId",
                table: "AssemblyTasks",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderBoxComponents_CatalogItemId",
                table: "OrderBoxComponents",
                column: "CatalogItemId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderBoxComponents_OrderBoxId",
                table: "OrderBoxComponents",
                column: "OrderBoxId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderBoxes_OrderId",
                table: "OrderBoxes",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderMarketplaceItems_OrderId",
                table: "OrderMarketplaceItems",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_CreatedById",
                table: "Orders",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_Number",
                table: "Orders",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Orders_WarehouseId",
                table: "Orders",
                column: "WarehouseId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AssemblyFulfillmentAssembledBundleComponentSnapshots");

            migrationBuilder.DropTable(
                name: "AssemblyFulfillmentBundleComponents");

            migrationBuilder.DropTable(
                name: "OrderBoxComponents");

            migrationBuilder.DropTable(
                name: "OrderMarketplaceItems");

            migrationBuilder.DropTable(
                name: "AssemblyFulfillments");

            migrationBuilder.DropTable(
                name: "AssemblyTaskBoxComponents");

            migrationBuilder.DropTable(
                name: "AssemblyTaskBoxes");

            migrationBuilder.DropTable(
                name: "AssemblyTasks");

            migrationBuilder.DropTable(
                name: "OrderBoxes");

            migrationBuilder.DropTable(
                name: "Orders");
        }
    }
}
